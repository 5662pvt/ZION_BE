using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Application.Options;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.Payments.Infrastructure.Gateway;
using ZIONShop.Payments.Infrastructure.Persistence;
using ZIONShop.Payments.Infrastructure.Repositories;

namespace ZIONShop.Payments.Infrastructure.DependencyInjection;

public static class PaymentsInfrastructureExtensions
{
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PaymentsDbContext.Schema)));

        // Eager read, same as ZIONShop.Auth.DependencyInjection.AuthBuildingBlockExtensions.AddZionAuth reading
        // JwtOptions: fail fast at startup rather than lazily on first request if the fake webhook secret is
        // missing outside Development, since an empty secret would make signature verification meaningless.
        var fakeOptions = configuration.GetSection(FakeGatewayOptions.SectionName).Get<FakeGatewayOptions>() ?? new FakeGatewayOptions();
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(fakeOptions.WebhookSecret))
            throw new InvalidOperationException("Payments:Fake:WebhookSecret must be configured outside Development.");

        services.Configure<FakeGatewayOptions>(configuration.GetSection(FakeGatewayOptions.SectionName));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPayableOrderRepository, PayableOrderRepository>();
        services.AddScoped<IProcessedWebhookEventRepository, ProcessedWebhookEventRepository>();
        services.AddScoped<IPaymentsUnitOfWork>(sp => sp.GetRequiredService<PaymentsDbContext>());
        services.AddScoped<IPaymentGateway, FakePaymentGateway>();

        services.AddOutbox<PaymentsDbContext>(configuration);
        services.AddInboxAdmin<PaymentsDbContext>();

        services.AddIntegrationEventHandler<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler, PaymentsDbContext>();
        services.AddIntegrationEventHandler<InventoryReservedIntegrationEvent, InventoryReservedIntegrationEventHandler, PaymentsDbContext>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler, PaymentsDbContext>();
        services.AddIntegrationEventHandler<InventoryReservationExpiredIntegrationEvent, InventoryReservationExpiredIntegrationEventHandler, PaymentsDbContext>();
        services.AddIntegrationEventHandler<InventoryReservationFailedIntegrationEvent, InventoryReservationFailedIntegrationEventHandler, PaymentsDbContext>();

        return services;
    }
}

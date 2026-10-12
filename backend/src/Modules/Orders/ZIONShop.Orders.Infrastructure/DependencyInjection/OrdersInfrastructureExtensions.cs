using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Payments;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Application.IntegrationEvents;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Readers;
using ZIONShop.Orders.Infrastructure.Repositories;
using ZIONShop.Orders.Infrastructure.Services;

namespace ZIONShop.Orders.Infrastructure.DependencyInjection;

public static class OrdersInfrastructureExtensions
{
    public static IServiceCollection AddOrdersInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OrdersDbContext.Schema)));

        services.Configure<CheckoutOptions>(configuration.GetSection(CheckoutOptions.SectionName));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrdersUnitOfWork>(sp => sp.GetRequiredService<OrdersDbContext>());
        services.AddScoped<IOrderDetailsReader, OrderDetailsReader>();
        services.AddScoped<IPurchaseVerifier, PurchaseVerifier>();

        services.AddOutbox<OrdersDbContext>();

        services.AddIntegrationEventHandler<InventoryReservedIntegrationEvent, InventoryReservedIntegrationEventHandler>();
        services.AddIntegrationEventHandler<InventoryReservationFailedIntegrationEvent, InventoryReservationFailedIntegrationEventHandler>();
        services.AddIntegrationEventHandler<InventoryReservationExpiredIntegrationEvent, InventoryReservationExpiredIntegrationEventHandler>();
        services.AddIntegrationEventHandler<PaymentCompletedIntegrationEvent, PaymentCompletedIntegrationEventHandler>();
        services.AddIntegrationEventHandler<PaymentFailedIntegrationEvent, PaymentFailedIntegrationEventHandler>();

        return services;
    }
}

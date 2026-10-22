using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Reviews;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Application.Services;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.Loyalty.Infrastructure.BackgroundServices;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.Repositories;
using ZIONShop.Loyalty.Infrastructure.Services;

namespace ZIONShop.Loyalty.Infrastructure.DependencyInjection;

public static class LoyaltyInfrastructureExtensions
{
    public static IServiceCollection AddLoyaltyInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LoyaltyDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", LoyaltyDbContext.Schema)));

        services.Configure<LoyaltyOptions>(configuration.GetSection(LoyaltyOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IPointAccountRepository, PointAccountRepository>();
        services.AddScoped<IPointLotRepository, PointLotRepository>();
        services.AddScoped<IPointTransactionRepository, PointTransactionRepository>();
        services.AddScoped<IPointReservationRepository, PointReservationRepository>();
        services.AddScoped<IOrderPointsRevocationRepository, OrderPointsRevocationRepository>();
        services.AddScoped<ILoyaltyUnitOfWork>(sp => sp.GetRequiredService<LoyaltyDbContext>());

        services.AddScoped<LoyaltyPointsService>();
        services.AddScoped<ILoyaltyPointsService>(sp => sp.GetRequiredService<LoyaltyPointsService>());
        services.AddScoped<ILoyaltyMutationService>(sp => sp.GetRequiredService<LoyaltyPointsService>());

        services.AddScoped<LoyaltyExpiryProcessor>();
        services.AddZionBackgroundService<LoyaltyExpiryBackgroundService>(configuration);

        services.AddOutbox<LoyaltyDbContext>(configuration);
        services.AddInboxAdmin<LoyaltyDbContext>();

        services.AddIntegrationEventHandler<OrderCompletedIntegrationEvent, OrderCompletedIntegrationEventHandler, LoyaltyDbContext>();
        services.AddIntegrationEventHandler<ReviewCreatedIntegrationEvent, ReviewCreatedIntegrationEventHandler, LoyaltyDbContext>();
        services.AddIntegrationEventHandler<OrderConfirmedIntegrationEvent, OrderConfirmedIntegrationEventHandler, LoyaltyDbContext>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler, LoyaltyDbContext>();
        services.AddIntegrationEventHandler<InventoryReservationFailedIntegrationEvent, InventoryReservationFailedIntegrationEventHandler, LoyaltyDbContext>();
        services.AddIntegrationEventHandler<OrderRefundedIntegrationEvent, OrderRefundedIntegrationEventHandler, LoyaltyDbContext>();

        return services;
    }
}

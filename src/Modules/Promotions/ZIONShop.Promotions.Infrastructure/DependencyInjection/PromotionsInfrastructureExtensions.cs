using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Promotions.Application.IntegrationEvents;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Services;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.BackgroundServices;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Readers;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Infrastructure.Services;

namespace ZIONShop.Promotions.Infrastructure.DependencyInjection;

public static class PromotionsInfrastructureExtensions
{
    public static IServiceCollection AddPromotionsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PromotionsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PromotionsDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IPromotionRepository, PromotionRepository>();
        services.AddScoped<ICouponCodeRepository, CouponCodeRepository>();
        services.AddScoped<ICouponRedemptionRepository, CouponRedemptionRepository>();
        services.AddScoped<IGiftRedemptionRepository, GiftRedemptionRepository>();
        services.AddScoped<IFlashSaleRepository, FlashSaleRepository>();
        services.AddScoped<IFlashSaleItemRepository, FlashSaleItemRepository>();
        services.AddScoped<IFlashSaleRedemptionRepository, FlashSaleRedemptionRepository>();
        services.AddScoped<IPromotionsUnitOfWork>(sp => sp.GetRequiredService<PromotionsDbContext>());
        services.AddScoped<IPromotionEvaluator, PromotionEvaluator>();
        services.AddScoped<IGiftPromotionEvaluator, GiftPromotionEvaluator>();
        services.AddScoped<IFlashSaleEvaluator, FlashSaleEvaluator>();
        services.AddScoped<IPromotionLookup, PromotionLookup>();

        services.AddScoped<StaleReservationReleaseProcessor>();
        services.AddZionBackgroundService<StaleReservationReleaseBackgroundService>(configuration);

        services.AddOutbox<PromotionsDbContext>(configuration);
        services.AddInboxAdmin<PromotionsDbContext>();

        services.AddIntegrationEventHandler<OrderConfirmedIntegrationEvent, OrderConfirmedIntegrationEventHandler, PromotionsDbContext>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler, PromotionsDbContext>();
        services.AddIntegrationEventHandler<InventoryReservationFailedIntegrationEvent, InventoryReservationFailedIntegrationEventHandler, PromotionsDbContext>();

        return services;
    }
}

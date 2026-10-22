using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.Inventory.Infrastructure.BackgroundServices;
using ZIONShop.Inventory.Infrastructure.Outbox;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Readers;
using ZIONShop.Inventory.Infrastructure.Repositories;

namespace ZIONShop.Inventory.Infrastructure.DependencyInjection;

public static class InventoryInfrastructureExtensions
{
    public static IServiceCollection AddInventoryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<InventoryDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema)));

        services.AddScoped<IInventoryItemRepository, InventoryItemRepository>();
        services.AddScoped<IStockReservationRepository, StockReservationRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IInventoryUnitOfWork>(sp => sp.GetRequiredService<InventoryDbContext>());
        services.AddScoped<IInventoryOutbox, InventoryOutbox>();
        services.AddScoped<IStockAvailabilityReader, StockAvailabilityReader>();
        services.AddScoped<IInventoryStatsReader, InventoryStatsReader>();
        services.AddScoped<IProductStockSummaryReader, ProductStockSummaryReader>();

        services.AddOutbox<InventoryDbContext>(configuration);
        services.AddInboxAdmin<InventoryDbContext>();
        services.AddZionBackgroundService<ReservationExpiryBackgroundService>(configuration);

        services.AddIntegrationEventHandler<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler, InventoryDbContext>();
        services.AddIntegrationEventHandler<OrderConfirmedIntegrationEvent, OrderConfirmedIntegrationEventHandler, InventoryDbContext>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler, InventoryDbContext>();
        services.AddIntegrationEventHandler<OrderReservationReleaseRequestedIntegrationEvent, OrderReservationReleaseRequestedIntegrationEventHandler, InventoryDbContext>();
        services.AddIntegrationEventHandler<OrderReturnReceivedIntegrationEvent, OrderReturnReceivedIntegrationEventHandler, InventoryDbContext>();
        services.AddIntegrationEventHandler<ProductUpdatedIntegrationEvent, ProductUpdatedIntegrationEventHandler, InventoryDbContext>();

        return services;
    }
}

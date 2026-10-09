using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Application.Services;

namespace ZIONShop.Payments.Application.DependencyInjection;

public static class PaymentsApplicationExtensions
{
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<FakeWebhookProcessor>();

        services.AddIntegrationEventHandler<OrderCreatedIntegrationEvent, OrderCreatedIntegrationEventHandler>();
        services.AddIntegrationEventHandler<InventoryReservedIntegrationEvent, InventoryReservedIntegrationEventHandler>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, OrderCancelledIntegrationEventHandler>();
        services.AddIntegrationEventHandler<InventoryReservationExpiredIntegrationEvent, InventoryReservationExpiredIntegrationEventHandler>();
        services.AddIntegrationEventHandler<InventoryReservationFailedIntegrationEvent, InventoryReservationFailedIntegrationEventHandler>();

        return services;
    }
}

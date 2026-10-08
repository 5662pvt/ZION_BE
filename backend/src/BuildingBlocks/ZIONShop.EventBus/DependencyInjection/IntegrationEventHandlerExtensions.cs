using Microsoft.Extensions.DependencyInjection;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.DependencyInjection;

public static class IntegrationEventHandlerExtensions
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> for <typeparamref name="TEvent"/> and records the event type
    /// in the shared <see cref="IntegrationEventTypeRegistry"/>, regardless of which <see cref="IEventBus"/>
    /// provider is active (InMemory resolves handlers directly; RabbitMQ uses the registry to bind queues).
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddTransient<IIntegrationEventHandler<TEvent>, THandler>();
        GetOrCreateRegistry(services).Register(typeof(TEvent));
        return services;
    }

    internal static IntegrationEventTypeRegistry GetOrCreateRegistry(IServiceCollection services)
    {
        var existing = services
            .FirstOrDefault(d => d.ServiceType == typeof(IntegrationEventTypeRegistry))
            ?.ImplementationInstance as IntegrationEventTypeRegistry;

        if (existing is not null) return existing;

        var registry = new IntegrationEventTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}

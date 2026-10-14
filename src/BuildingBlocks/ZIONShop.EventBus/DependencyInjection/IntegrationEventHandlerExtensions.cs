using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.DependencyInjection;

public static class IntegrationEventHandlerExtensions
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> for <typeparamref name="TEvent"/> and records the event type
    /// in the shared <see cref="IntegrationEventTypeRegistry"/>, regardless of which <see cref="IEventBus"/>
    /// provider is active (InMemory resolves handlers directly; RabbitMQ uses the registry to bind queues).
    /// Not idempotent on redelivery -- prefer the <typeparamref name="TDbContext"/> overload below.
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddTransient<IIntegrationEventHandler<TEvent>, THandler>();
        GetOrCreateRegistry(services).Register(typeof(TEvent));
        return services;
    }

    /// <summary>
    /// Same as <see cref="AddIntegrationEventHandler{TEvent,THandler}"/>, but wraps <typeparamref name="THandler"/>
    /// in <see cref="IdempotentIntegrationEventHandler{TEvent,THandler,TDbContext}"/> so redelivery of the same
    /// <see cref="IntegrationEvent.EventId"/> is skipped instead of re-run (inbox pattern, keyed on
    /// (EventId, consumer), recorded in <typeparamref name="TDbContext"/>).
    /// </summary>
    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler, TDbContext>(this IServiceCollection services)
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
        where TDbContext : DbContext
    {
        services.AddTransient<THandler>();
        services.AddTransient<IIntegrationEventHandler<TEvent>, IdempotentIntegrationEventHandler<TEvent, THandler, TDbContext>>();
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

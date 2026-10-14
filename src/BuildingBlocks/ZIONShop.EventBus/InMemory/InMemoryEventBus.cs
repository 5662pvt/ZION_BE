using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.InMemory;

public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider provider, ILogger<InMemoryEventBus> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
    {
        using var scope = _provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>().ToList();
        if (handlers.Count == 0)
        {
            _logger.LogDebug("No handler for {EventType}", typeof(TEvent).Name);
            return;
        }
        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integration handler {Handler} failed for {EventType}", handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}

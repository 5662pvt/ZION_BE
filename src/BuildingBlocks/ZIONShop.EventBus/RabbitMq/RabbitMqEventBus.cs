using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqEventBus : IEventBus
{
    private readonly RabbitMqPublisher _publisher;
    private readonly ILogger<RabbitMqEventBus> _logger;

    public RabbitMqEventBus(RabbitMqPublisher publisher, ILogger<RabbitMqEventBus> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
    {
        var routingKey = typeof(TEvent).Name;
        var body = JsonSerializer.SerializeToUtf8Bytes<object>(@event);
        try
        {
            await _publisher.PublishEventAsync(routingKey, @event.EventId.ToString(), body, cancellationToken);
            _logger.LogInformation("Published {EventType} (EventId {EventId}), confirmed by broker", routingKey, @event.EventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType} (EventId {EventId}); rethrowing so the outbox can retry", routingKey, @event.EventId);
            throw;
        }
    }
}

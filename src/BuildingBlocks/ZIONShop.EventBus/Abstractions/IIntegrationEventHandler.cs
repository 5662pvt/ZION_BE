namespace ZIONShop.EventBus.Abstractions;

public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    /// <summary>Delivery is at-least-once (RabbitMQ retry/outbox replay) — implementations must be idempotent, keyed on <see cref="IntegrationEvent.EventId"/>.</summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

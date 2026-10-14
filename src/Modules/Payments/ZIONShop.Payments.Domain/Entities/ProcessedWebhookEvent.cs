namespace ZIONShop.Payments.Domain.Entities;

/// <summary>Idempotency marker for inbound fake-gateway webhook deliveries, keyed by the provider's EventId.</summary>
public class ProcessedWebhookEvent
{
    private ProcessedWebhookEvent() { }

    public Guid EventId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    public static ProcessedWebhookEvent Create(Guid eventId, DateTime processedAt) =>
        new() { EventId = eventId, ProcessedAt = processedAt };
}

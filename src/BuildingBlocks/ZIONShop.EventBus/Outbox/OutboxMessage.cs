namespace ZIONShop.EventBus.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime OccurredOn { get; set; }
    public DateTime? ProcessedOn { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }

    /// <summary>W3C traceparent of the activity active when this event was added to the outbox --
    /// lets <c>OutboxPublisherService</c> resume the same trace when it eventually publishes.</summary>
    public string? TraceParent { get; set; }
    public string? TraceState { get; set; }
}

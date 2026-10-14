namespace ZIONShop.EventBus.Inbox;

public class InboxMessage
{
    public Guid MessageId { get; set; }
    public string Consumer { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedOn { get; set; }
}

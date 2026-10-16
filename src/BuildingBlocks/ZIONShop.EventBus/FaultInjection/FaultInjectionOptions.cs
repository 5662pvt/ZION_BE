namespace ZIONShop.EventBus.FaultInjection;

public class FaultInjectionOptions
{
    public const string SectionName = "FaultInjection";

    /// <summary>Event type name (short class name, e.g. <c>OrderCreatedIntegrationEvent</c> --
    /// matched case-insensitively) that the RabbitMQ consumer should crash the process on, right
    /// after its handler(s) succeed but before the message is ack'd. Empty disables the hook.
    /// Only honored when the host environment is Development -- see <see cref="CrashBeforeAckFaultInjector"/>.</summary>
    public string? CrashBeforeAckEventType { get; set; }
}

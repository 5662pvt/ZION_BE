namespace ZIONShop.EventBus.FaultInjection;

/// <summary>
/// Fallback used when a composition root wires up <see cref="RabbitMq.RabbitMqConsumerService"/>
/// without going through <c>AddZionEventBus</c>'s RabbitMq branch (e.g. a test host building its own
/// <c>ServiceCollection</c>) -- never crashes, so fault injection stays opt-in to hosts that actually
/// register <see cref="CrashBeforeAckFaultInjector"/>.
/// </summary>
public sealed class NoOpFaultInjector : IFaultInjector
{
    public static readonly NoOpFaultInjector Instance = new();

    public bool ShouldCrashBeforeAck(string eventType) => false;

    public void Crash(string eventType, string? messageId)
    {
    }
}

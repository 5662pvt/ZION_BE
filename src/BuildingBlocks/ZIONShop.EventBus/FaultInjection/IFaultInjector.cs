namespace ZIONShop.EventBus.FaultInjection;

public interface IFaultInjector
{
    bool ShouldCrashBeforeAck(string eventType);

    void Crash(string eventType, string? messageId);
}

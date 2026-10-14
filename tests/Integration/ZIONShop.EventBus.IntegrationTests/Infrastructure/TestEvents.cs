using System.Collections.Concurrent;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

public sealed record TestIntegrationEvent(string Payload) : IntegrationEvent;

public sealed record AlwaysFailsIntegrationEvent(string Payload) : IntegrationEvent;

/// <summary>Shared across every DI scope the consumer creates per message, so it can observe
/// deliveries/attempts across the whole test regardless of how many scopes were involved.</summary>
public sealed class EventRecorder
{
    private readonly ConcurrentBag<IntegrationEvent> _received = new();
    private int _attempts;

    public IReadOnlyCollection<IntegrationEvent> Received => _received.ToArray();
    public int Attempts => _attempts;

    public void Record(IntegrationEvent @event) => _received.Add(@event);

    public void RecordAttempt(IntegrationEvent @event)
    {
        Interlocked.Increment(ref _attempts);
        Record(@event);
    }
}

public sealed class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
    private readonly EventRecorder _recorder;
    public TestIntegrationEventHandler(EventRecorder recorder) => _recorder = recorder;

    public Task HandleAsync(TestIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _recorder.Record(@event);
        return Task.CompletedTask;
    }
}

public sealed class AlwaysFailsIntegrationEventHandler : IIntegrationEventHandler<AlwaysFailsIntegrationEvent>
{
    private readonly EventRecorder _recorder;
    public AlwaysFailsIntegrationEventHandler(EventRecorder recorder) => _recorder = recorder;

    public Task HandleAsync(AlwaysFailsIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _recorder.RecordAttempt(@event);
        throw new InvalidOperationException("Simulated handler failure.");
    }
}

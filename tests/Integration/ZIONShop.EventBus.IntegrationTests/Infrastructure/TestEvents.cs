using System.Collections.Concurrent;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

public sealed record TestIntegrationEvent(string Payload) : IntegrationEvent;

public sealed record AlwaysFailsIntegrationEvent(string Payload) : IntegrationEvent;

/// <summary>Carries no payload of its own -- handlers key their per-attempt state off <see cref="IntegrationEvent.EventId"/>.</summary>
public sealed record MultiSaveIntegrationEvent : IntegrationEvent;

public sealed record TwoHandlersIntegrationEvent : IntegrationEvent;

/// <summary>Shared, per-EventId attempt counter so a handler can behave differently on its first vs.
/// later delivery attempts (simulating "fails once, then succeeds").</summary>
public sealed class AttemptTracker
{
    private readonly ConcurrentDictionary<Guid, int> _attempts = new();
    public int NextAttempt(Guid eventId) => _attempts.AddOrUpdate(eventId, 1, (_, v) => v + 1);
}

/// <summary>
/// Saves effect "A", then on the first attempt for a given EventId throws AFTER tracking an effect "B"
/// but BEFORE saving it -- exercising the IdempotentIntegrationEventHandler decorator's one-transaction
/// guarantee: if the whole delivery fails, even the earlier "A" save (already flushed to the DB, just not
/// committed because it is inside the decorator's explicit transaction) must be rolled back too.
/// </summary>
public sealed class MultiSaveHandler : IIntegrationEventHandler<MultiSaveIntegrationEvent>
{
    private readonly EventBusTestDbContext _db;
    private readonly AttemptTracker _attempts;

    public MultiSaveHandler(EventBusTestDbContext db, AttemptTracker attempts)
    {
        _db = db;
        _attempts = attempts;
    }

    public async Task HandleAsync(MultiSaveIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _db.Effects.Add(new TestEffect { Id = Guid.NewGuid(), Name = "A" });
        await _db.SaveChangesAsync(cancellationToken);

        var attempt = _attempts.NextAttempt(@event.EventId);

        _db.Effects.Add(new TestEffect { Id = Guid.NewGuid(), Name = "B" });
        if (attempt == 1)
            throw new InvalidOperationException("Simulated failure after the first SaveChangesAsync.");

        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Always throws -- used to assert that a handler which never succeeds leaves nothing
/// committed, not even the one effect it saved before throwing.</summary>
public sealed class AlwaysThrowsAfterSaveHandler : IIntegrationEventHandler<MultiSaveIntegrationEvent>
{
    private readonly EventBusTestDbContext _db;

    public AlwaysThrowsAfterSaveHandler(EventBusTestDbContext db) => _db = db;

    public async Task HandleAsync(MultiSaveIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _db.Effects.Add(new TestEffect { Id = Guid.NewGuid(), Name = "A" });
        await _db.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException("Simulated permanent handler failure.");
    }
}

/// <summary>First handler for <see cref="TwoHandlersIntegrationEvent"/>: throws on the first delivery
/// attempt, succeeds (recording its own inbox-dedup'd effect) from the second attempt onward.</summary>
public sealed class FirstThenSucceedsHandler : IIntegrationEventHandler<TwoHandlersIntegrationEvent>
{
    private readonly EventBusTestDbContext _db;
    private readonly AttemptTracker _attempts;

    public FirstThenSucceedsHandler(EventBusTestDbContext db, AttemptTracker attempts)
    {
        _db = db;
        _attempts = attempts;
    }

    public async Task HandleAsync(TwoHandlersIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var attempt = _attempts.NextAttempt(@event.EventId);
        if (attempt == 1)
            throw new InvalidOperationException("Simulated transient failure on first delivery.");

        _db.Effects.Add(new TestEffect { Id = Guid.NewGuid(), Name = "FirstHandler" });
        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Second handler for <see cref="TwoHandlersIntegrationEvent"/>: always succeeds. Used to prove
/// the consumer still invokes every registered handler for a delivery even when an earlier one throws.</summary>
public sealed class AlwaysSucceedsHandler : IIntegrationEventHandler<TwoHandlersIntegrationEvent>
{
    private readonly EventBusTestDbContext _db;

    public AlwaysSucceedsHandler(EventBusTestDbContext db) => _db = db;

    public async Task HandleAsync(TwoHandlersIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        _db.Effects.Add(new TestEffect { Id = Guid.NewGuid(), Name = "SecondHandler" });
        await _db.SaveChangesAsync(cancellationToken);
    }
}

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

/// <summary>Swapped in for the real <see cref="IEventBus"/> (via <c>EventBusTestHost.CreateAsync</c>'s
/// <c>configureServices</c> seam) to simulate a publish failure that is the message's own fault -- e.g. a
/// serialization bug -- rather than a broker outage. <see cref="OutboxPublisherService{TDbContext}"/> must
/// treat this distinctly from <c>BrokerUnavailableException</c>: increment <c>Attempts</c>, record
/// <c>Error</c>, and leave the row unprocessed, instead of stopping the batch without penalty.</summary>
public sealed class ThrowingEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent =>
        throw new InvalidOperationException("Simulated non-broker publish failure.");
}

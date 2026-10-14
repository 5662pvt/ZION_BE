using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;

namespace ZIONShop.EventBus.Tests;

public sealed record SampleIntegrationEvent(string Message) : IntegrationEvent;

public sealed record OtherIntegrationEvent(int Value) : IntegrationEvent;

public class SampleIntegrationEventHandler : IIntegrationEventHandler<SampleIntegrationEvent>
{
    public Task HandleAsync(SampleIntegrationEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class OtherIntegrationEventHandler : IIntegrationEventHandler<OtherIntegrationEvent>
{
    public Task HandleAsync(OtherIntegrationEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutbox("test");
}

public class RecordingEventBus : IEventBus
{
    public List<IntegrationEvent> Published { get; } = new();
    public bool ShouldThrow { get; set; }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated publish failure.");
        Published.Add(@event);
        return Task.CompletedTask;
    }
}

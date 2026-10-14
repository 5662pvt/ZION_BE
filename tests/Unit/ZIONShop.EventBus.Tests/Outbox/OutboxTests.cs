using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Outbox;

namespace ZIONShop.EventBus.Tests.Outbox;

public class OutboxTests
{
    [Fact]
    public async Task Add_Should_PersistMessageWithTypeAndContent_When_SavedAsync()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
        await using var db = new TestDbContext(options);
        var outbox = new Outbox<TestDbContext>(db);
        var @event = new SampleIntegrationEvent("hello");

        outbox.Add(@event);
        await db.SaveChangesAsync();

        var stored = await db.Set<OutboxMessage>().SingleAsync();
        stored.Id.Should().Be(@event.EventId);
        stored.Type.Should().Be(typeof(SampleIntegrationEvent).AssemblyQualifiedName);
        stored.Content.Should().Contain("hello");
        stored.ProcessedOn.Should().BeNull();
        stored.Attempts.Should().Be(0);
    }
}

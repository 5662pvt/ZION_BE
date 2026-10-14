using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZIONShop.EventBus.Outbox;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.EventBus.Tests.Outbox;

public class OutboxPublisherServiceTests
{
    private static (ServiceProvider provider, RecordingEventBus bus) BuildProvider()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var bus = new RecordingEventBus();

        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<Abstractions.IEventBus>(bus);

        return (services.BuildServiceProvider(), bus);
    }

    private static OutboxPublisherService<TestDbContext> CreatePublisher(IServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RabbitMqOptions()),
            NullLogger<OutboxPublisherService<TestDbContext>>.Instance);

    [Fact]
    public async Task PublishBatchAsync_Should_MarkProcessedAndPublish_When_PublishSucceeds()
    {
        var (provider, bus) = BuildProvider();
        await using var _ = provider;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            new Outbox<TestDbContext>(db).Add(new SampleIntegrationEvent("hello"));
            await db.SaveChangesAsync();
        }

        var publisher = CreatePublisher(provider);
        await publisher.PublishBatchAsync(CancellationToken.None);

        bus.Published.Should().ContainSingle().Which.Should().BeOfType<SampleIntegrationEvent>();

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var message = await verifyDb.Set<OutboxMessage>().SingleAsync();
        message.ProcessedOn.Should().NotBeNull();
        message.Attempts.Should().Be(0);
        message.Error.Should().BeNull();
    }

    [Fact]
    public async Task PublishBatchAsync_Should_IncrementAttemptsAndKeepUnprocessed_When_PublishFails()
    {
        var (provider, bus) = BuildProvider();
        await using var _ = provider;
        bus.ShouldThrow = true;

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            new Outbox<TestDbContext>(db).Add(new SampleIntegrationEvent("boom"));
            await db.SaveChangesAsync();
        }

        var publisher = CreatePublisher(provider);
        await publisher.PublishBatchAsync(CancellationToken.None);

        bus.Published.Should().BeEmpty();

        using var verifyScope = provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TestDbContext>();
        var message = await verifyDb.Set<OutboxMessage>().SingleAsync();
        message.ProcessedOn.Should().BeNull();
        message.Attempts.Should().Be(1);
        message.Error.Should().NotBeNullOrEmpty();
    }
}

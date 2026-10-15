using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.IntegrationTests.Infrastructure;
using ZIONShop.EventBus.Outbox;
using ZIONShop.EventBus.RabbitMq;
using static ZIONShop.EventBus.IntegrationTests.Infrastructure.Eventually;

namespace ZIONShop.EventBus.IntegrationTests;

[Collection(EventBusTestCollection.Name)]
public class EventBusTests
{
    private readonly EventBusContainers _containers;

    public EventBusTests(EventBusContainers containers)
    {
        _containers = containers;
    }

    [Fact]
    public async Task PublishAsync_Should_DeliverToHandlerExactlyOnce_When_PublishSucceeds()
    {
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres);
        await host.StartConsumerAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        var @event = new TestIntegrationEvent("hello");
        await bus.PublishAsync(@event);

        await WaitUntilAsync(() => host.Recorder.Received.Count >= 1, TimeSpan.FromSeconds(10));

        host.Recorder.Received.Should().ContainSingle()
            .Which.Should().BeOfType<TestIntegrationEvent>()
            .Subject.As<TestIntegrationEvent>().EventId.Should().Be(@event.EventId);
    }

    [Fact]
    public async Task HandlerAlwaysThrows_Should_RetryThenDeadLetter_When_RetryCountExceeded()
    {
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres,
            o =>
            {
                o.RetryCount = 2;
                o.RetryDelaysSeconds = new[] { 1, 1, 1 };
            });
        await host.StartConsumerAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new AlwaysFailsIntegrationEvent("boom"));

        // One initial attempt + RetryCount delayed retries, then dead-lettered.
        await WaitUntilAsync(() => host.Recorder.Attempts >= host.Options.RetryCount + 1, TimeSpan.FromSeconds(30));
        host.Recorder.Attempts.Should().Be(host.Options.RetryCount + 1);

        using var channel = host.OpenRawChannel();
        var dlqName = RabbitMqTopology.DeadLetterQueueName(host.Options.QueueName);

        BasicGetResult? result = null;
        await WaitUntilAsync(() =>
        {
            result = channel.BasicGet(dlqName, autoAck: true);
            return result is not null;
        }, TimeSpan.FromSeconds(10));

        result.Should().NotBeNull();
        result!.BasicProperties.Headers.Should().ContainKey("x-death");
    }

    [Fact]
    public async Task UnregisteredEventType_Should_GoStraightToDeadLetterQueue_When_Consumed()
    {
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres);
        await host.StartConsumerAsync();

        // No handler/registry entry exists for this "type" -- simulates a legacy/foreign event
        // the consumer's binding still covers but it can no longer (or never could) deserialize.
        const string unknownType = "SomeEventTypeNobodyKnowsAbout";
        using var channel = host.OpenRawChannel();
        channel.QueueBind(host.Options.QueueName, host.Options.Exchange, unknownType);

        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = Guid.NewGuid().ToString();
        props.Type = unknownType;
        var body = Encoding.UTF8.GetBytes("{\"foo\":\"bar\"}");
        channel.BasicPublish(host.Options.Exchange, unknownType, props, body);

        var dlqName = RabbitMqTopology.DeadLetterQueueName(host.Options.QueueName);
        BasicGetResult? result = null;
        await WaitUntilAsync(() =>
        {
            result = channel.BasicGet(dlqName, autoAck: true);
            return result is not null;
        }, TimeSpan.FromSeconds(10));

        result.Should().NotBeNull();
        result!.BasicProperties.Type.Should().Be(unknownType);
    }

    [Fact]
    public async Task PublishBatchAsync_Should_FailFastWithoutPenalty_When_BrokerDown_Then_RecoverAfterRestart()
    {
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres);

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox<EventBusTestDbContext>>();
            outbox.Add(new TestIntegrationEvent("row-1"));
            outbox.Add(new TestIntegrationEvent("row-2"));
            outbox.Add(new TestIntegrationEvent("row-3"));
            await db.SaveChangesAsync();
        }

        await _containers.RabbitMq.StopAsync();
        try
        {
            var publisher = host.CreateOutboxPublisher();
            var stopwatch = Stopwatch.StartNew();
            await publisher.PublishBatchAsync(CancellationToken.None);
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            var rows = await db.Set<OutboxMessage>().ToListAsync();
            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(m => m.ProcessedOn == null && m.Attempts == 0);
        }
        finally
        {
            await _containers.RabbitMq.StartAsync();
        }

        await WaitUntilAsync(async () =>
        {
            var publisher = host.CreateOutboxPublisher();
            await publisher.PublishBatchAsync(CancellationToken.None);

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            return await db.Set<OutboxMessage>().CountAsync(m => m.ProcessedOn != null) == 3;
        }, TimeSpan.FromSeconds(20));

        await host.StartConsumerAsync();
        await WaitUntilAsync(() => host.Recorder.Received.Count(e => e is TestIntegrationEvent) >= 3, TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// Reproduces the live regression: unlike
    /// <see cref="PublishBatchAsync_Should_FailFastWithoutPenalty_When_BrokerDown_Then_RecoverAfterRestart"/>
    /// (consumer only starts *after* the broker is already back), here the consumer is already
    /// subscribed and the publisher's channel already open *before* the broker goes down, so
    /// RabbitMQ.Client raises ConsumerCancelled on the live connection -- the exact path that used to
    /// dispose the still-autorecovering connection, which then threw ObjectDisposedException on the
    /// next property access and both broke the consumer's reinit for good and poisoned the publisher's
    /// Attempts counter.
    /// </summary>
    [Fact]
    public async Task Consumer_Should_StayConnected_And_Publisher_Should_NeverBurnAttempts_When_BrokerRestartsWhileSubscribed()
    {
        var logCapture = new TestLogCapture();
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres,
            configureServices: services =>
            {
                services.AddSingleton(logCapture);
                services.AddSingleton(typeof(ILogger<>), typeof(CapturingLogger<>));
            });

        await host.StartConsumerAsync();

        // Prove the consumer is subscribed and the publisher's channel is open before the outage.
        var bus = host.Services.GetRequiredService<IEventBus>();
        await bus.PublishAsync(new TestIntegrationEvent("warmup"));
        await WaitUntilAsync(
            () => host.Recorder.Received.Any(e => e is TestIntegrationEvent te && te.Payload == "warmup"),
            TimeSpan.FromSeconds(10));

        await _containers.RabbitMq.StopAsync();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox<EventBusTestDbContext>>();
            outbox.Add(new TestIntegrationEvent("row-1"));
            outbox.Add(new TestIntegrationEvent("row-2"));
            outbox.Add(new TestIntegrationEvent("row-3"));
            await db.SaveChangesAsync();
        }

        try
        {
            var publisher = host.CreateOutboxPublisher();
            var pollCount = 0;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
            while (pollCount < 10 && DateTime.UtcNow < deadline)
            {
                await publisher.PublishBatchAsync(CancellationToken.None);
                pollCount++;
                await Task.Delay(TimeSpan.FromSeconds(1.5));
            }

            pollCount.Should().BeGreaterThanOrEqualTo(10,
                "the publisher must complete at least 10 PublishBatchAsync calls while the broker is down " +
                "within a generous 90s budget -- a machine under load should just take longer, not fail");

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            var rows = await db.Set<OutboxMessage>().ToListAsync();
            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(m => m.ProcessedOn == null && m.Attempts == 0);

            logCapture.AnyObjectDisposedException.Should().BeFalse(
                "a broker outage must never surface ObjectDisposedException -- that means our code disposed " +
                "a connection the client library's own autorecovery still owned");
        }
        finally
        {
            await _containers.RabbitMq.StartAsync();
        }

        await WaitUntilAsync(async () =>
        {
            var publisher = host.CreateOutboxPublisher();
            await publisher.PublishBatchAsync(CancellationToken.None);

            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            return await db.Set<OutboxMessage>().CountAsync(m => m.ProcessedOn != null) == 3;
        }, TimeSpan.FromSeconds(60));

        // Proves the consumer reconnected (and resubscribed) rather than being left dangling.
        await WaitUntilAsync(
            () => host.Recorder.Received.Count(e => e is TestIntegrationEvent) >= 4, // warmup + 3 rows
            TimeSpan.FromSeconds(30));

        var connections = await _containers.RabbitMq.ExecAsync(new[] { "rabbitmqctl", "list_connections", "-q" });
        // -q only suppresses the "Listing connections ..."/"...done" banner -- the "user  peer_host
        // peer_port  state" column header line is still the first line regardless.
        var connectionLines = connections.Stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .ToArray();
        connectionLines.Should().HaveCount(1,
            "the app must end up with exactly one RabbitMQ connection -- a leaked second connection " +
            "means our code raced the client's own autorecovery instead of waiting for it");
    }

    [Fact]
    public async Task Topology_Should_SelfHeal_When_QueueAlreadyExistsWithoutDeadLetterArgs()
    {
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres);

        using (var channel = host.OpenRawChannel())
        {
            channel.ExchangeDeclare(host.Options.Exchange, ExchangeType.Topic, durable: true);
            channel.QueueDeclare(host.Options.QueueName, durable: true, exclusive: false, autoDelete: false);
        }

        var bus = host.Services.GetRequiredService<IEventBus>();
        var publish = async () => await bus.PublishAsync(new TestIntegrationEvent("after-migration"));
        await publish.Should().NotThrowAsync();

        await host.StartConsumerAsync();
        await WaitUntilAsync(() => host.Recorder.Received.Count >= 1, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task HandleAsync_Should_RollBackEverySave_When_LaterSaveInTheSameDeliveryThrows()
    {
        var attempts = new AttemptTracker();
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres,
            configureServices: services =>
            {
                services.AddSingleton(attempts);
                services.AddIntegrationEventHandler<MultiSaveIntegrationEvent, MultiSaveHandler, EventBusTestDbContext>();
            });

        var @event = new MultiSaveIntegrationEvent();

        // First attempt: handler saves effect "A" (flushed to the DB but not yet committed, since the
        // decorator's explicit transaction is still open), then throws before saving effect "B".
        using (var scope = host.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<MultiSaveIntegrationEvent>>();
            var act = async () => await handler.HandleAsync(@event, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            // The whole transaction rolled back -- effect "A" must not have survived either, and no
            // inbox row was left behind to make a retry think this delivery already happened.
            (await db.Effects.CountAsync()).Should().Be(0);
            (await db.Set<InboxMessage>().CountAsync(m => m.MessageId == @event.EventId)).Should().Be(0);
        }

        // Retry (redelivery): both saves succeed this time.
        using (var scope = host.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<MultiSaveIntegrationEvent>>();
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            var names = await db.Effects.Select(e => e.Name).ToListAsync();
            names.Should().BeEquivalentTo(new[] { "A", "B" });
            (await db.Set<InboxMessage>().CountAsync(m => m.MessageId == @event.EventId)).Should().Be(1);
        }
    }

    [Fact]
    public async Task HandleAsync_Should_CommitNothing_When_HandlerThrowsAfterItsOwnSave()
    {
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres,
            configureServices: services =>
                services.AddIntegrationEventHandler<MultiSaveIntegrationEvent, AlwaysThrowsAfterSaveHandler, EventBusTestDbContext>());

        var @event = new MultiSaveIntegrationEvent();

        using (var scope = host.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler<MultiSaveIntegrationEvent>>();
            var act = async () => await handler.HandleAsync(@event, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        using var verifyScope = host.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
        (await db.Effects.CountAsync()).Should().Be(0);
        (await db.Set<InboxMessage>().CountAsync(m => m.MessageId == @event.EventId)).Should().Be(0);
    }

    [Fact]
    public async Task Consumer_Should_RunEveryHandler_When_AnEarlierOneThrowsOnFirstDelivery_Then_SkipOnlyTheSucceededOneOnRetry()
    {
        var attempts = new AttemptTracker();
        await using var host = await EventBusTestHost.CreateAsync(_containers.RabbitMq, _containers.Postgres,
            o =>
            {
                o.RetryCount = 3;
                o.RetryDelaysSeconds = new[] { 1, 1, 1 };
            },
            services =>
            {
                services.AddSingleton(attempts);
                services.AddIntegrationEventHandler<TwoHandlersIntegrationEvent, FirstThenSucceedsHandler, EventBusTestDbContext>();
                services.AddIntegrationEventHandler<TwoHandlersIntegrationEvent, AlwaysSucceedsHandler, EventBusTestDbContext>();
            });
        await host.StartConsumerAsync();

        var bus = host.Services.GetRequiredService<IEventBus>();
        var @event = new TwoHandlersIntegrationEvent();
        await bus.PublishAsync(@event);

        // First delivery: FirstThenSucceedsHandler throws, but AlwaysSucceedsHandler must still run and
        // commit its effect (and its own inbox row) rather than being skipped because a sibling handler
        // failed. The failed delivery as a whole gets retried via the retry queue; on that retry,
        // AlwaysSucceedsHandler is skipped by its own inbox (already processed) while
        // FirstThenSucceedsHandler succeeds and records its effect for the first time.
        await WaitUntilAsync(async () =>
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            return await db.Effects.CountAsync(e => e.Name == "FirstHandler") == 1
                && await db.Effects.CountAsync(e => e.Name == "SecondHandler") == 1;
        }, TimeSpan.FromSeconds(20));

        using var verifyScope = host.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
        (await verifyDb.Effects.CountAsync(e => e.Name == "SecondHandler")).Should().Be(1); // not re-run on retry
        (await verifyDb.Effects.CountAsync(e => e.Name == "FirstHandler")).Should().Be(1);
        (await verifyDb.Set<InboxMessage>().CountAsync(m => m.MessageId == @event.EventId)).Should().Be(2); // one row per consumer
    }
}

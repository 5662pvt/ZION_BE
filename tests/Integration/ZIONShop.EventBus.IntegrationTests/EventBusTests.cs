using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using ZIONShop.EventBus.Abstractions;
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
}

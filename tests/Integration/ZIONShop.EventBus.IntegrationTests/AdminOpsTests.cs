using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Primitives;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.IntegrationTests.Infrastructure;
using ZIONShop.EventBus.RabbitMq;
using ZIONShop.HealthChecks;
using static ZIONShop.EventBus.IntegrationTests.Infrastructure.Eventually;

namespace ZIONShop.EventBus.IntegrationTests;

/// <summary>Covers the admin "Hệ thống" DLQ tooling (<see cref="IDeadLetterAdmin"/>) and the
/// <see cref="WorkerHealthCheck"/> against a real RabbitMQ broker -- the InMemory-provider paths for
/// both are covered where they're cheap to reach without a container (DeadLetterAdmin's InMemory
/// implementation, WorkerHealthCheck's InMemory short-circuit).</summary>
[Collection(EventBusTestCollection.Name)]
public class AdminOpsTests
{
    private readonly EventBusContainers _containers;

    public AdminOpsTests(EventBusContainers containers) => _containers = containers;

    private static Task<EventBusTestHost> CreateHostAsync(EventBusContainers containers, Action<RabbitMqOptions>? configure = null) =>
        EventBusTestHost.CreateAsync(containers.RabbitMq, containers.Postgres,
            o =>
            {
                o.RetryCount = 1;
                o.RetryDelaysSeconds = new[] { 1 };
                configure?.Invoke(o);
            },
            services => services.AddSingleton<IDeadLetterAdmin, RabbitMqDeadLetterAdmin>());

    private static async Task<Guid> PublishPoisonedMessageAsync(EventBusTestHost host, string payload)
    {
        var bus = host.Services.GetRequiredService<IEventBus>();
        var @event = new AlwaysFailsIntegrationEvent(payload);
        await bus.PublishAsync(@event);
        return @event.EventId;
    }

    [Fact]
    public async Task PeekAsync_Should_ReturnDeathInfo_And_LeaveTheMessageInTheQueue()
    {
        await using var host = await CreateHostAsync(_containers);
        await host.StartConsumerAsync();
        var deadLetterAdmin = host.Services.GetRequiredService<IDeadLetterAdmin>();

        await PublishPoisonedMessageAsync(host, "peek-me");
        await WaitUntilAsync(() => host.Recorder.Attempts >= host.Options.RetryCount + 1, TimeSpan.FromSeconds(30));

        DeadLetterPeekResult? result = null;
        await WaitUntilAsync(async () =>
        {
            result = await deadLetterAdmin.PeekAsync(10, CancellationToken.None);
            return result.Messages.Count >= 1;
        }, TimeSpan.FromSeconds(10));

        var message = result!.Messages.Should().ContainSingle(m => m.EventType == nameof(AlwaysFailsIntegrationEvent)).Subject;
        message.DeathCount.Should().BeGreaterThanOrEqualTo(1);
        message.FirstDeathAt.Should().NotBeNull();
        message.Reason.Should().NotBe("unknown");

        // Peeking must never remove anything -- a second peek must still see the same message.
        var secondPeek = await deadLetterAdmin.PeekAsync(10, CancellationToken.None);
        secondPeek.Messages.Should().Contain(m => m.MessageId == message.MessageId);
    }

    [Fact]
    public async Task RequeueAsync_Should_MoveMessageBackOntoTheMainQueue_And_ConsumerReceivesItAgain()
    {
        await using var host = await CreateHostAsync(_containers);
        await host.StartConsumerAsync();
        var deadLetterAdmin = host.Services.GetRequiredService<IDeadLetterAdmin>();

        await PublishPoisonedMessageAsync(host, "requeue-me");
        await WaitUntilAsync(() => host.Recorder.Attempts >= host.Options.RetryCount + 1, TimeSpan.FromSeconds(30));
        var attemptsBeforeRequeue = host.Recorder.Attempts;

        await WaitUntilAsync(async () => (await deadLetterAdmin.PeekAsync(10, CancellationToken.None)).Messages.Count >= 1, TimeSpan.FromSeconds(10));

        var requeued = await deadLetterAdmin.RequeueAsync(null, CancellationToken.None);
        requeued.Should().Be(1);

        // The requeued message is a fresh publish to the main exchange -- the still-running consumer
        // must pick it up, retry it (it always fails), and dead-letter it again.
        await WaitUntilAsync(() => host.Recorder.Attempts > attemptsBeforeRequeue, TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task DeleteAsync_Should_RemoveOnlyTheMatchingMessage()
    {
        await using var host = await CreateHostAsync(_containers);
        await host.StartConsumerAsync();
        var deadLetterAdmin = host.Services.GetRequiredService<IDeadLetterAdmin>();

        var keepId = await PublishPoisonedMessageAsync(host, "keep-me");
        var deleteId = await PublishPoisonedMessageAsync(host, "delete-me");

        await WaitUntilAsync(() => host.Recorder.Attempts >= 2 * (host.Options.RetryCount + 1), TimeSpan.FromSeconds(30));

        DeadLetterPeekResult? before = null;
        await WaitUntilAsync(async () =>
        {
            before = await deadLetterAdmin.PeekAsync(10, CancellationToken.None);
            return before.Messages.Count >= 2;
        }, TimeSpan.FromSeconds(10));

        var deleted = await deadLetterAdmin.DeleteAsync(deleteId.ToString(), CancellationToken.None);
        deleted.Should().Be(1);

        var after = await deadLetterAdmin.PeekAsync(10, CancellationToken.None);
        after.Messages.Should().Contain(m => m.MessageId == keepId.ToString());
        after.Messages.Should().NotContain(m => m.MessageId == deleteId.ToString());
    }

    [Fact]
    public async Task DeleteAsync_Should_Return0_When_MessageIdDoesNotMatchAnything()
    {
        await using var host = await CreateHostAsync(_containers);
        var deadLetterAdmin = host.Services.GetRequiredService<IDeadLetterAdmin>();

        var deleted = await deadLetterAdmin.DeleteAsync(Guid.NewGuid().ToString(), CancellationToken.None);

        deleted.Should().Be(0);
    }

    [Fact]
    public async Task WorkerHealthCheck_Should_BeHealthyWithConsumerCount_WhileConsumerRuns_Then_Unhealthy_AfterItDisconnects()
    {
        await using var host = await CreateHostAsync(_containers);
        await host.StartConsumerAsync();

        var configuration = new FakeConfiguration(new Dictionary<string, string?> { ["EventBus:Provider"] = "RabbitMq" });
        var healthCheck = new WorkerHealthCheck(configuration, host.Services);

        HealthCheckResult result = default;
        await WaitUntilAsync(async () =>
        {
            result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
            return result.Status == HealthStatus.Healthy;
        }, TimeSpan.FromSeconds(10));
        result.Description.Should().StartWith("consumers=");

        // Disposing the consumer service closes the channel its BasicConsume ran on, which is what
        // actually drops the broker-side consumer -- BackgroundService.StopAsync alone would not,
        // since RabbitMqConsumerService.ExecuteAsync already returned right after subscribing.
        host.Services.GetRequiredService<RabbitMqConsumerService>().Dispose();

        await WaitUntilAsync(async () =>
        {
            result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
            return result.Status == HealthStatus.Unhealthy;
        }, TimeSpan.FromSeconds(10));
        result.Description.Should().Be("no consumer: start the worker");
    }

    [Fact]
    public async Task WorkerHealthCheck_Should_ReportHealthy_When_ProviderIsInMemory()
    {
        var configuration = new FakeConfiguration(new Dictionary<string, string?> { ["EventBus:Provider"] = "InMemory" });
        var healthCheck = new WorkerHealthCheck(configuration, new ServiceCollection().BuildServiceProvider());

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("in-memory provider");
    }

    private sealed class FakeConfiguration : IConfiguration
    {
        private readonly Dictionary<string, string?> _values;
        public FakeConfiguration(Dictionary<string, string?> values) => _values = values;

        public string? this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : null;
            set => _values[key] = value;
        }

        public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
        public IEnumerable<IConfigurationSection> GetChildren() => throw new NotSupportedException();
        public IChangeToken GetReloadToken() => throw new NotSupportedException();
    }
}

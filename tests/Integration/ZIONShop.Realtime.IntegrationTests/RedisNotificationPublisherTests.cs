using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using ZIONShop.Realtime.IntegrationTests.Infrastructure;

namespace ZIONShop.Realtime.IntegrationTests;

[Collection(RedisTestCollection.Name)]
public class RedisNotificationPublisherTests
{
    private readonly RedisContainerFixture _fixture;

    public RedisNotificationPublisherTests(RedisContainerFixture fixture) => _fixture = fixture;

    private static UserNotificationPushDto SampleDto() =>
        new(Guid.NewGuid(), "OrderConfirmed", "Title", "Message", Guid.NewGuid(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task PublishAsync_Should_NotThrow_When_RedisIsReachable()
    {
        using var multiplexer = _fixture.Connect();
        var publisher = new RedisNotificationPublisher(multiplexer, NullLogger<RedisNotificationPublisher>.Instance);

        var act = async () => await publisher.PublishAsync(Guid.NewGuid(), SampleDto());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_Should_FailOpen_When_RedisIsDown()
    {
        using var multiplexer = _fixture.Connect(timeoutMs: 300);
        var publisher = new RedisNotificationPublisher(multiplexer, NullLogger<RedisNotificationPublisher>.Instance);

        await _fixture.Redis.StopAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var act = async () => await publisher.PublishAsync(Guid.NewGuid(), SampleDto());

            await act.Should().NotThrowAsync();
            stopwatch.Stop();

            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        }
        finally
        {
            await _fixture.Redis.StartAsync();
        }
    }

    [Fact]
    public async Task PublishAsync_Should_DeliverToSubscriber_When_RedisIsReachable()
    {
        using var publisherConn = _fixture.Connect();
        using var subscriberConn = _fixture.Connect();
        var userId = Guid.NewGuid();
        var publisher = new RedisNotificationPublisher(publisherConn, NullLogger<RedisNotificationPublisher>.Instance);
        var received = new TaskCompletionSource<RedisValue>(TaskCreationOptions.RunContinuationsAsynchronously);

        await subscriberConn.GetSubscriber().SubscribeAsync(RedisChannel.Literal($"zion:notifications:{userId}"), (_, message) => received.TrySetResult(message));

        var dto = SampleDto();
        await publisher.PublishAsync(userId, dto);

        var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        completed.Should().Be(received.Task);
        var message = await received.Task;
        message.ToString().Should().Contain(dto.Id.ToString());
    }
}

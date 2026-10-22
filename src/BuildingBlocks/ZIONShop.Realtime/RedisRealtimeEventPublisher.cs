using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ZIONShop.Realtime;

/// <summary>
/// Fail-open contract: same rationale as <see cref="RedisNotificationPublisher"/> -- a Redis outage
/// (down, unreachable, or slow) must never throw out of <see cref="PublishToUserAsync"/>/
/// <see cref="PublishToStaffAsync"/> and must never block the caller beyond a short, bounded budget.
/// Publishes a JSON envelope <c>{ event, payload }</c> (web casing) to <c>zion:rt:user:{userId}</c> or
/// <c>zion:rt:staff</c>; relayed to SignalR connections by <c>RealtimeEventRelayHostedService</c> (Api
/// host only).
/// </summary>
public class RedisRealtimeEventPublisher : IRealtimeEventPublisher
{
    private static readonly TimeSpan PublishBudget = TimeSpan.FromMilliseconds(500);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRealtimeEventPublisher> _logger;

    public RedisRealtimeEventPublisher(IConnectionMultiplexer redis, ILogger<RedisRealtimeEventPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default) =>
        PublishAsync($"zion:rt:user:{userId}", eventName, payload, ct);

    public Task PublishToStaffAsync(string eventName, object payload, CancellationToken ct = default) =>
        PublishAsync("zion:rt:staff", eventName, payload, ct);

    private async Task PublishAsync(string channelName, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            var channel = RedisChannel.Literal(channelName);
            var envelope = JsonSerializer.Serialize(new { @event = eventName, payload }, JsonOptions);
            var publish = _redis.GetSubscriber().PublishAsync(channel, envelope);
            var completed = await Task.WhenAny(publish, Task.Delay(PublishBudget, ct));
            if (completed != publish)
            {
                _logger.LogWarning("Realtime event publish on {Channel} exceeded {BudgetMs}ms; failing open", channelName, PublishBudget.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime event publish on {Channel} failed; failing open", channelName);
        }
    }
}

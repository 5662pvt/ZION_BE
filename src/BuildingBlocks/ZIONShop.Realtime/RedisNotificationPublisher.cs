using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ZIONShop.Realtime;

/// <summary>
/// Fail-open contract: a Redis outage (down, unreachable, or slow) must never throw out of
/// <see cref="PublishAsync"/> and must never block the caller beyond a short, bounded budget.
/// The underlying <see cref="IConnectionMultiplexer"/> is configured with short Connect/Sync/Async
/// timeouts (same approach as <c>ZIONShop.Caching.RedisCacheService</c>) and every failure --
/// including one that would otherwise block past the budget -- is caught and logged as a Warning
/// instead of propagating. Business transactions (saving a <c>UserNotification</c>) must complete
/// regardless of whether the real-time push actually reached a subscriber.
/// </summary>
public class RedisNotificationPublisher : INotificationPush
{
    private static readonly TimeSpan PublishBudget = TimeSpan.FromMilliseconds(500);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisNotificationPublisher> _logger;

    public RedisNotificationPublisher(IConnectionMultiplexer redis, ILogger<RedisNotificationPublisher> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task PublishAsync(Guid userId, UserNotificationPushDto notification, CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = RedisChannel.Literal($"zion:notifications:{userId}");
            var payload = JsonSerializer.Serialize(notification, JsonOptions);
            var publish = _redis.GetSubscriber().PublishAsync(channel, payload);
            var completed = await Task.WhenAny(publish, Task.Delay(PublishBudget, cancellationToken));
            if (completed != publish)
            {
                _logger.LogWarning("Redis notification publish for user {UserId} exceeded {BudgetMs}ms; failing open", userId, PublishBudget.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis notification publish for user {UserId} failed; failing open", userId);
        }
    }
}

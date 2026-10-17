using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using ZIONShop.Api.Hubs;
using ZIONShop.Realtime;

namespace ZIONShop.Api.Realtime;

/// <summary>
/// Api-host-only relay: subscribes to the Redis pub/sub channel pattern <c>zion:notifications:*</c>
/// (published by <c>RedisNotificationPublisher</c>, which may run in this same process or in the
/// Worker process -- see module docs) and forwards each message to the matching user's SignalR
/// connection, if one is open on this Api instance. Registered unconditionally, independent of
/// <c>BackgroundProcessing:Enabled</c> -- delivery only ever happens from the Api host, since that's
/// the only process holding SignalR connections. Must never crash the Api host: a Redis outage at
/// startup or mid-flight degrades to "no real-time delivery" (clients fall back to polling the feed
/// REST endpoints), retried with a fixed backoff, while REST traffic keeps being served normally.
/// </summary>
public class NotificationRelayHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const string ChannelPrefix = "zion:notifications:";

    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly ILogger<NotificationRelayHostedService> _logger;

    public NotificationRelayHostedService(IConnectionMultiplexer redis, IHubContext<NotificationsHub> hub, ILogger<NotificationRelayHostedService> logger)
    {
        _redis = redis;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var subscriber = _redis.GetSubscriber();
                await subscriber.SubscribeAsync(RedisChannel.Pattern(ChannelPrefix + "*"), OnMessage);

                // Push-based: OnMessage fires as messages arrive. Stay parked here until the
                // connection drops or we're asked to stop, then loop back and resubscribe.
                while (!stoppingToken.IsCancellationRequested && _redis.IsConnected)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notification relay subscription failed; retrying in {DelaySeconds}s", RetryDelay.TotalSeconds);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnMessage(RedisChannel channel, RedisValue message) => _ = HandleMessageAsync(channel, message);

    private async Task HandleMessageAsync(RedisChannel channel, RedisValue message)
    {
        try
        {
            var channelName = channel.ToString();
            var idx = channelName.IndexOf(ChannelPrefix, StringComparison.Ordinal);
            if (idx < 0 || !Guid.TryParse(channelName[(idx + ChannelPrefix.Length)..], out var userId))
            {
                _logger.LogWarning("Notification relay received message on unrecognized channel {Channel}", channelName);
                return;
            }

            var dto = JsonSerializer.Deserialize<UserNotificationPushDto>((string)message!, JsonOptions);
            if (dto is null) return;

            await _hub.Clients.User(userId.ToString()).SendAsync("notificationReceived", dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to relay notification message on channel {Channel}", channel);
        }
    }
}

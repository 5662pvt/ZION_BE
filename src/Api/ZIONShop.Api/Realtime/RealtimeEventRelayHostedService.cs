using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using ZIONShop.Api.Hubs;

namespace ZIONShop.Api.Realtime;

/// <summary>
/// Api-host-only relay for the generic <c>IRealtimeEventPublisher</c> channels, modelled on
/// <see cref="NotificationRelayHostedService"/> (fail-open, resubscribe loop). Subscribes to
/// <c>zion:rt:user:*</c> and <c>zion:rt:staff</c> and forwards each <c>{ event, payload }</c> envelope
/// to the matching <see cref="ChatHub"/> connection(s) on this Api instance.
/// </summary>
public class RealtimeEventRelayHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const string UserChannelPrefix = "zion:rt:user:";
    private const string StaffChannel = "zion:rt:staff";

    private readonly IConnectionMultiplexer _redis;
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<RealtimeEventRelayHostedService> _logger;

    public RealtimeEventRelayHostedService(IConnectionMultiplexer redis, IHubContext<ChatHub> hub, ILogger<RealtimeEventRelayHostedService> logger)
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
                await subscriber.SubscribeAsync(RedisChannel.Pattern(UserChannelPrefix + "*"), OnMessage);
                await subscriber.SubscribeAsync(RedisChannel.Literal(StaffChannel), OnMessage);

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
                _logger.LogWarning(ex, "Chat realtime relay subscription failed; retrying in {DelaySeconds}s", RetryDelay.TotalSeconds);
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
            var envelope = JsonSerializer.Deserialize<RealtimeEnvelope>((string)message!, JsonOptions);
            if (envelope is null) return;

            var channelName = channel.ToString();
            if (channelName == StaffChannel)
            {
                await _hub.Clients.Group("staff").SendAsync(envelope.Event, envelope.Payload);
                return;
            }

            var idx = channelName.IndexOf(UserChannelPrefix, StringComparison.Ordinal);
            if (idx < 0 || !Guid.TryParse(channelName[(idx + UserChannelPrefix.Length)..], out var userId))
            {
                _logger.LogWarning("Chat realtime relay received message on unrecognized channel {Channel}", channelName);
                return;
            }

            await _hub.Clients.User(userId.ToString()).SendAsync(envelope.Event, envelope.Payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to relay realtime event message on channel {Channel}", channel);
        }
    }

    private sealed record RealtimeEnvelope(string Event, JsonElement Payload);
}

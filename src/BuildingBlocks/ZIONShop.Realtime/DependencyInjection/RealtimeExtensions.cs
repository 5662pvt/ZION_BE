using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ZIONShop.Realtime.DependencyInjection;

public static class RealtimeExtensions
{
    /// <summary>
    /// Registers an independent <see cref="IConnectionMultiplexer"/> (not shared with
    /// <c>ZIONShop.Caching</c>) plus <see cref="INotificationPush"/>. Reads the same
    /// <c>Redis:Configuration</c> key as the Caching building block, but must work even when
    /// <c>Caching:Enabled=false</c> -- real-time push is an independent feature, not a cache.
    /// </summary>
    public static IServiceCollection AddZionRealtime(this IServiceCollection services, IConfiguration configuration)
    {
        var redisSection = configuration.GetSection("Redis");
        var connection = redisSection["Configuration"] ?? "localhost:6379";
        var connectTimeoutMs = redisSection.GetValue<int?>("ConnectTimeoutMs") ?? 1000;
        var syncTimeoutMs = redisSection.GetValue<int?>("SyncTimeoutMs") ?? 500;
        var asyncTimeoutMs = redisSection.GetValue<int?>("AsyncTimeoutMs") ?? 500;

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connection, true);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = connectTimeoutMs;
            options.SyncTimeout = syncTimeoutMs;
            options.AsyncTimeout = asyncTimeoutMs;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<INotificationPush, RedisNotificationPublisher>();

        return services;
    }
}

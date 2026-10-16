using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ZIONShop.Caching.DependencyInjection;

public static class CachingExtensions
{
    public static IServiceCollection AddZionCaching(this IServiceCollection services, IConfiguration configuration)
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
        services.AddSingleton<IDistributedCacheService, RedisCacheService>();
        return services;
    }
}

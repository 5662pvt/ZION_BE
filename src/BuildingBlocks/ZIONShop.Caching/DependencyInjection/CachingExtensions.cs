using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ZIONShop.Caching.DependencyInjection;

public static class CachingExtensions
{
    public static IServiceCollection AddZionCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetSection("Redis")["Configuration"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(connection, true);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = 2000;
            options.SyncTimeout = 2000;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<IDistributedCacheService, RedisCacheService>();
        return services;
    }
}

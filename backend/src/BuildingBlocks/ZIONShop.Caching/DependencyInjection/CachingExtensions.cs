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
            ConnectionMultiplexer.Connect(ConfigurationOptions.Parse(connection, true)));
        services.AddSingleton<IDistributedCacheService, RedisCacheService>();
        return services;
    }
}

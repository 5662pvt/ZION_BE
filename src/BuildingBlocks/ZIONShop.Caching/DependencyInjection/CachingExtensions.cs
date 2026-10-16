using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ZIONShop.Caching.DependencyInjection;

public static class CachingExtensions
{
    public static IServiceCollection AddZionCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var cachingEnabled = configuration.GetValue<bool?>("Caching:Enabled") ?? true;
        if (!cachingEnabled)
        {
            services.AddSingleton<IDistributedCacheService, NullDistributedCacheService>();
            services.AddHostedService<CachingDisabledStartupLogger>();
            return services;
        }

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

    private sealed class CachingDisabledStartupLogger : IHostedService
    {
        private readonly ILogger<CachingDisabledStartupLogger> _logger;

        public CachingDisabledStartupLogger(ILogger<CachingDisabledStartupLogger> logger) => _logger = logger;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Distributed cache disabled (Caching:Enabled=false)");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

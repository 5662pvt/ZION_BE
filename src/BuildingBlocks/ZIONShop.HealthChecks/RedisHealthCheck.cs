using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace ZIONShop.HealthChecks;

/// <summary>Redis is optional (the app fails open when it's unavailable) -- so an unreachable
/// Redis degrades readiness instead of failing it, and a Redis that was never registered at all
/// (e.g. not configured for this environment) is reported healthy, not degraded.</summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _serviceProvider;

    public RedisHealthCheck(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var multiplexer = _serviceProvider.GetService<IConnectionMultiplexer>();
        if (multiplexer is null) return HealthCheckResult.Healthy("not configured");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            await multiplexer.GetDatabase().PingAsync().WaitAsync(cts.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception)
        {
            return HealthCheckResult.Degraded("Redis unreachable");
        }
    }
}

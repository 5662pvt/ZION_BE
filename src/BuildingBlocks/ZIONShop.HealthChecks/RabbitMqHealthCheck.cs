using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.HealthChecks;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public RabbitMqHealthCheck(IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var provider = _configuration[$"{RabbitMqOptions.SectionName}:Provider"] ?? "InMemory";
        if (!string.Equals(provider, "RabbitMq", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HealthCheckResult.Healthy("in-memory provider"));

        // A connected check alone is a false negative here: this host may have no consumer (e.g. the
        // API with BackgroundProcessing:Enabled=false), so RabbitMqConnection never opens until the
        // first publish. Fall back to a bounded connect attempt (TryGetConnection's own
        // RequestedConnectionTimeout) before reporting Unhealthy, same as WorkerHealthCheck does.
        var connection = _serviceProvider.GetService<RabbitMqConnection>();
        var result = connection is not null && (connection.IsConnected || connection.TryGetConnection(out _))
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ not connected");
        return Task.FromResult(result);
    }
}

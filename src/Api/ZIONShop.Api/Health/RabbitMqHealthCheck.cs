using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.Api.Health;

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

        var connection = _serviceProvider.GetService<RabbitMqConnection>();
        var result = connection is { IsConnected: true }
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("RabbitMQ not connected");
        return Task.FromResult(result);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.HealthChecks;

/// <summary>
/// Reports whether anything is actually consuming the main queue -- a broker that is up (see
/// <see cref="RabbitMqHealthCheck"/>) with zero consumers means events pile up in the outbox/queue
/// forever because nobody ever started the Worker (or the API with BackgroundProcessing enabled).
/// </summary>
public class WorkerHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public WorkerHealthCheck(IConfiguration configuration, IServiceProvider serviceProvider)
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
        if (connection is null || !connection.TryGetConnection(out var rabbitConnection))
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ not connected"));

        var queueName = _serviceProvider.GetRequiredService<IOptions<RabbitMqOptions>>().Value.QueueName;

        IModel? channel = null;
        try
        {
            channel = rabbitConnection.CreateModel();
            var result = channel.QueueDeclarePassive(queueName);
            var healthResult = result.ConsumerCount >= 1
                ? HealthCheckResult.Healthy($"consumers={result.ConsumerCount}")
                : HealthCheckResult.Unhealthy("no consumer: start the worker");
            return Task.FromResult(healthResult);
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ not connected", ex));
        }
        finally
        {
            try { channel?.Dispose(); } catch { /* already closed by the broker */ }
        }
    }
}

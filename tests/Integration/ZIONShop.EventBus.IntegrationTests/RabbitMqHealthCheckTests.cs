using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZIONShop.EventBus.IntegrationTests.Infrastructure;
using ZIONShop.EventBus.RabbitMq;
using ZIONShop.HealthChecks;

namespace ZIONShop.EventBus.IntegrationTests;

/// <summary>
/// Regression tests for the API's /health/ready false negative: with no consumer running (e.g. the
/// API with BackgroundProcessing:Enabled=false), <see cref="RabbitMqConnection"/> never opens until
/// the first publish, so a plain <c>IsConnected</c> check reported Unhealthy even with the broker
/// up. RabbitMqHealthCheck must now fall back to a bounded connect attempt before giving up.
/// </summary>
[Collection(EventBusTestCollection.Name)]
public class RabbitMqHealthCheckTests
{
    private readonly EventBusContainers _containers;

    public RabbitMqHealthCheckTests(EventBusContainers containers)
    {
        _containers = containers;
    }

    private ServiceProvider BuildProvider()
    {
        var options = new RabbitMqOptions
        {
            Provider = "RabbitMq",
            Host = _containers.RabbitMq.Hostname,
            // A fixed host port binding (see EventBusContainers) -- unlike GetMappedPublicPort(5672),
            // this stays valid even while the container is stopped, which the unreachable-broker test
            // below needs.
            Port = EventBusContainers.RabbitMqHostPort,
            User = "guest",
            Pass = "guest",
        };

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<RabbitMqOptions>>(Options.Create(options));
        services.AddSingleton<RabbitMqConnection>();
        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EventBus:Provider"] = "RabbitMq" })
            .Build();

    [Fact]
    public async Task CheckHealthAsync_Should_ReportHealthy_When_ConnectionIsFreshButBrokerReachable()
    {
        using var provider = BuildProvider();
        var healthCheck = new RabbitMqHealthCheck(BuildConfiguration(), provider);

        // RabbitMqConnection has never been used yet (no publish, no consumer) -- IsConnected alone
        // would be false here even though the broker is perfectly reachable.
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_Should_ReportUnhealthy_When_BrokerUnreachable()
    {
        await _containers.RabbitMq.StopAsync();
        try
        {
            using var provider = BuildProvider();
            var healthCheck = new RabbitMqHealthCheck(BuildConfiguration(), provider);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            result.Status.Should().Be(HealthStatus.Unhealthy);
        }
        finally
        {
            await _containers.RabbitMq.StartAsync();
        }
    }
}

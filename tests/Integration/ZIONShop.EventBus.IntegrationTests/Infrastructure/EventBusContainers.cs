using System.Net;
using System.Net.Sockets;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

/// <summary>Shared RabbitMQ + Postgres containers for the whole collection -- each test isolates
/// itself with a unique exchange/queue name and outbox schema, so containers only start once.</summary>
public sealed class EventBusContainers : IAsyncLifetime
{
    // Testcontainers 3.10.0 has no Pause/UnpauseAsync for IContainer, so the broker-outage test
    // uses Stop/StartAsync on this same container instead. A fixed host port binding keeps the
    // mapped port (and thus RabbitMqOptions.Port) stable across that restart -- a fresh dynamic
    // mapping would otherwise break any connection/options captured before the restart.
    // The port is picked at runtime (instead of a hard-coded literal) because Windows/Hyper-V's
    // WinNAT dynamic port-exclusion ranges shift on every reboot, so any fixed literal can land
    // inside an excluded range and make Docker refuse to bind it.
    public static readonly int RabbitMqHostPort = GetAvailablePort();

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .WithPortBinding(RabbitMqHostPort, 5672)
        .Build();

    public PostgreSqlContainer Postgres { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("zionshop_eventbus_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(RabbitMq.StartAsync(), Postgres.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(RabbitMq.DisposeAsync().AsTask(), Postgres.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class EventBusTestCollection : ICollectionFixture<EventBusContainers>
{
    public const string Name = "EventBus containers";
}

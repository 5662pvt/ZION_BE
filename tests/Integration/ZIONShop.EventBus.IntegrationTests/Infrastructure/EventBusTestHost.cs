using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Outbox;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal hand-built DI host wiring the real RabbitMq/outbox classes -- mirrors what
/// AddZionEventBus + AddOutbox do in production (see EventBusExtensions,
/// OutboxServiceCollectionExtensions) but built by hand so each test can use short retry delays
/// and an isolated exchange/queue/schema instead of the app's real appsettings.
/// </summary>
public sealed class EventBusTestHost : IAsyncDisposable
{
    public ServiceProvider Services { get; }
    public RabbitMqOptions Options { get; }
    public EventRecorder Recorder { get; }

    private EventBusTestHost(ServiceProvider services, RabbitMqOptions options, EventRecorder recorder)
    {
        Services = services;
        Options = options;
        Recorder = recorder;
    }

    public static async Task<EventBusTestHost> CreateAsync(
        RabbitMqContainer rabbitMq,
        PostgreSqlContainer postgres,
        Action<RabbitMqOptions>? configure = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var options = new RabbitMqOptions
        {
            Provider = "RabbitMq",
            Host = rabbitMq.Hostname,
            Port = rabbitMq.GetMappedPublicPort(5672),
            User = "guest",
            Pass = "guest",
            Exchange = $"test.events.{suffix}",
            QueueName = $"test.queue.{suffix}",
            RetryCount = 2,
            RetryDelaysSeconds = new[] { 1, 1, 1 },
            PrefetchCount = 20,
            ConfirmTimeoutSeconds = 5,
            OutboxPollSeconds = 1
        };
        configure?.Invoke(options);

        var services = new ServiceCollection();
        // Avoids pulling in the concrete Microsoft.Extensions.Logging package just for test
        // wiring -- NullLogger<T> (from the Abstractions package, already a transitive
        // dependency) is enough since these tests assert on behavior, not log output.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IOptions<RabbitMqOptions>>(Microsoft.Extensions.Options.Options.Create(options));

        var recorder = new EventRecorder();
        services.AddSingleton(recorder);

        services.AddIntegrationEventHandler<TestIntegrationEvent, TestIntegrationEventHandler>();
        services.AddIntegrationEventHandler<AlwaysFailsIntegrationEvent, AlwaysFailsIntegrationEventHandler>();

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        services.AddSingleton<RabbitMqConsumerService>();

        var schema = "t" + suffix;
        var dbOptions = new DbContextOptionsBuilder<EventBusTestDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        services.AddScoped(_ => new EventBusTestDbContext(dbOptions, schema));
        services.AddScoped<IOutbox<EventBusTestDbContext>, Outbox<EventBusTestDbContext>>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EventBusTestDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        return new EventBusTestHost(provider, options, recorder);
    }

    public OutboxPublisherService<EventBusTestDbContext> CreateOutboxPublisher() =>
        new(Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(Options),
            Services.GetRequiredService<ILogger<OutboxPublisherService<EventBusTestDbContext>>>());

    public Task StartConsumerAsync() =>
        Services.GetRequiredService<RabbitMqConsumerService>().StartAsync(CancellationToken.None);

    /// <summary>A throwaway channel on the same (already-connected) connection used by the
    /// publisher/consumer -- for test setup/assertions (manual binds, BasicGet on the DLQ, etc.)
    /// that shouldn't go through the production publish path.</summary>
    public IModel OpenRawChannel() =>
        Services.GetRequiredService<RabbitMqConnection>().GetConnection().CreateModel();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
    }
}

using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Inventory.Infrastructure.BackgroundServices;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.BackgroundServices;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.BackgroundServices;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Payments.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.BackgroundServices;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Persistence;

namespace ZIONShop.Worker.Tests;

/// <summary>
/// Builds the real Worker host (src/Worker/ZIONShop.Worker/Program.cs) with
/// ASPNETCORE_ENVIRONMENT=Development against a real Postgres + RabbitMQ (Testcontainers) --
/// unlike <see cref="WorkerHostTests"/>, which deliberately uses the "Testing" environment to skip
/// host-building validation, Development turns on ValidateOnBuild/ValidateScopes. This is the
/// regression test for the DI-validation failure the Worker hit on startup in Development
/// ("Unable to resolve service for type 'IJwtTokenService' while attempting to activate
/// 'LoginCommandHandler'") -- see Program.cs's AddZionAuth registration.
/// </summary>
public sealed class WorkerDevelopmentHostTests : IAsyncLifetime
{
    private static readonly Type[] GatedHostedServiceTypes =
    {
        typeof(OutboxPublisherService<InventoryDbContext>),
        typeof(OutboxPublisherService<PromotionsDbContext>),
        typeof(OutboxPublisherService<OrdersDbContext>),
        typeof(OutboxPublisherService<PaymentsDbContext>),
        typeof(OutboxPublisherService<LoyaltyDbContext>),
        typeof(OutboxPublisherService<ReviewsDbContext>),
        typeof(ReservationExpiryBackgroundService),
        typeof(StaleReservationReleaseBackgroundService),
        typeof(IdempotencyKeyCleanupBackgroundService),
        typeof(LoyaltyExpiryBackgroundService),
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("zionshop_worker_dev_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("EventBus__Provider", "RabbitMq");
        Environment.SetEnvironmentVariable("EventBus__Host", _rabbitMq.Hostname);
        Environment.SetEnvironmentVariable("EventBus__Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("EventBus__VirtualHost", "/");
        Environment.SetEnvironmentVariable("EventBus__User", "guest");
        Environment.SetEnvironmentVariable("EventBus__Pass", "guest");
        Environment.SetEnvironmentVariable("EventBus__Exchange", "worker.dev.test.events");
        Environment.SetEnvironmentVariable("EventBus__QueueName", "worker.dev.test.queue");
        // Redis is optional/fail-open (RedisHealthCheck reports Degraded, not Unhealthy, when
        // unreachable) -- point it at a closed local port instead of skipping it, so this test also
        // proves /health/ready tolerates a down Redis exactly like it must in Development.
        Environment.SetEnvironmentVariable("Redis__Configuration", "127.0.0.1:1");
        // PaymentsInfrastructureExtensions fails fast outside Development (checked via the raw
        // ASPNETCORE_ENVIRONMENT process variable, not IHostEnvironment -- UseEnvironment below
        // never touches it) unless this is set; a real secret is unnecessary for this host to merely
        // build, validate, and serve /health/*.
        Environment.SetEnvironmentVariable("Payments__Fake__WebhookSecret", "worker-dev-test-fake-secret");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public async Task Should_StartAndReportReady_WithValidateOnBuild_InDevelopment()
    {
        using var client = _factory!.CreateClient();

        var live = await client.GetAsync("/health/live");
        live.StatusCode.Should().Be(HttpStatusCode.OK);

        var ready = await client.GetAsync("/health/ready");
        ready.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var hostedServiceTypes = scope.ServiceProvider.GetServices<IHostedService>().Select(s => s.GetType()).ToHashSet();

        foreach (var type in GatedHostedServiceTypes)
            hostedServiceTypes.Should().Contain(type, $"{type.Name} should always run on the Worker host");
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("EventBus__Provider", null);
        Environment.SetEnvironmentVariable("EventBus__Host", null);
        Environment.SetEnvironmentVariable("EventBus__Port", null);
        Environment.SetEnvironmentVariable("EventBus__VirtualHost", null);
        Environment.SetEnvironmentVariable("EventBus__User", null);
        Environment.SetEnvironmentVariable("EventBus__Pass", null);
        Environment.SetEnvironmentVariable("EventBus__Exchange", null);
        Environment.SetEnvironmentVariable("EventBus__QueueName", null);
        Environment.SetEnvironmentVariable("Redis__Configuration", null);
        Environment.SetEnvironmentVariable("Payments__Fake__WebhookSecret", null);
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }
}

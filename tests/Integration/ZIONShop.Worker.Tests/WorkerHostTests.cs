using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Inventory.Infrastructure.BackgroundServices;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.BackgroundServices;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.BackgroundServices;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Payments.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.BackgroundServices;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Persistence;

namespace ZIONShop.Worker.Tests;

/// <summary>
/// Builds the real Worker host (src/Worker/ZIONShop.Worker/Program.cs) end to end -- every module's
/// Application+Infrastructure, Observability, EventBus -- without a real Postgres/RabbitMQ/Redis:
/// registering a DbContext via UseNpgsql never connects eagerly, EventBus:Provider=InMemory avoids
/// needing a broker, the "Testing" environment skips Redis entirely (same switch Program.cs/both
/// hosts use), and /health/live (asserted below) never runs the dependency checks anyway
/// (Predicate = _ => false) -- this only proves the host builds, every gated background job is
/// registered (BackgroundProcessing is forced on in Worker's Program.cs), and it serves traffic.
/// </summary>
public class WorkerHostTests : IDisposable
{
    private static readonly Type[] GatedHostedServiceTypes =
    {
        typeof(OutboxPublisherService<InventoryDbContext>),
        typeof(OutboxPublisherService<ProductsDbContext>),
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

    private readonly WebApplicationFactory<Program> _factory;

    public WorkerHostTests()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Host=127.0.0.1;Port=5432;Database=zionshop_worker_tests;Username=postgres;Password=postgres");
        Environment.SetEnvironmentVariable("EventBus__Provider", "InMemory");
        // PaymentsInfrastructureExtensions fails fast outside Development (checked via the raw
        // ASPNETCORE_ENVIRONMENT process variable, not IHostEnvironment -- UseEnvironment("Testing")
        // below never touches it) unless this is set; a real secret is unnecessary for this host to
        // merely build and register its services.
        Environment.SetEnvironmentVariable("Payments__Fake__WebhookSecret", "worker-tests-fake-secret");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task HealthLive_Should_Return200()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Should_RegisterEveryGatedBackgroundJob()
    {
        using var scope = _factory.Services.CreateScope();
        var hostedServiceTypes = scope.ServiceProvider.GetServices<IHostedService>().Select(s => s.GetType()).ToHashSet();

        foreach (var type in GatedHostedServiceTypes)
            hostedServiceTypes.Should().Contain(type, $"{type.Name} should always run on the Worker host");
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
        Environment.SetEnvironmentVariable("EventBus__Provider", null);
        Environment.SetEnvironmentVariable("Payments__Fake__WebhookSecret", null);
    }
}

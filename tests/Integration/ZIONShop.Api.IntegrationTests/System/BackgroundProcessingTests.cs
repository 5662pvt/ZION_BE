using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZIONShop.Api.IntegrationTests.Infrastructure;
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

namespace ZIONShop.Api.IntegrationTests.SystemOps;

/// <summary>
/// Every <c>Add&lt;Module&gt;Infrastructure</c>/<c>AddOutbox</c>/<c>AddZionEventBus</c> call reads
/// "BackgroundProcessing:Enabled" eagerly while Program.cs's top-level statements run -- the same
/// class of eager read ApiFactory.ConfigureWebHost already documents for Email:Host/connection
/// strings -- so, like those, it must be forced via a real process environment variable and a fresh
/// host build, not <c>ConfigureAppConfiguration</c> (which only takes effect once
/// WebApplicationFactory intercepts <c>Build()</c>, too late for an eager read). RabbitMqConsumerService
/// is intentionally not asserted on here: this whole suite forces EventBus:Provider=InMemory
/// (ApiFactory.ConfigureWebHost), under which that hosted service is never registered regardless of
/// BackgroundProcessing -- its gating is exercised by the same code path, just unreachable from this
/// particular host configuration.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class BackgroundProcessingTests
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

    private readonly ApiFactory _factory;

    public BackgroundProcessingTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public void Default_Should_RegisterEveryGatedBackgroundJob()
    {
        // The shared ApiFactory host never sets BackgroundProcessing:Enabled -- default true, same
        // behaviour as the single-process dev run before this option existed.
        using var scope = _factory.Services.CreateScope();
        var hostedServiceTypes = scope.ServiceProvider.GetServices<IHostedService>().Select(s => s.GetType()).ToHashSet();

        foreach (var type in GatedHostedServiceTypes)
            hostedServiceTypes.Should().Contain(type, $"{type.Name} should run by default");
    }

    [Fact]
    public void BackgroundProcessingDisabled_Should_RegisterNoneOfTheGatedBackgroundJobs()
    {
        Environment.SetEnvironmentVariable("BackgroundProcessing__Enabled", "false");
        try
        {
            using var factory = _factory.WithWebHostBuilder(_ => { });
            using var scope = factory.Services.CreateScope();
            var hostedServiceTypes = scope.ServiceProvider.GetServices<IHostedService>().Select(s => s.GetType()).ToHashSet();

            foreach (var type in GatedHostedServiceTypes)
                hostedServiceTypes.Should().NotContain(type, $"{type.Name} should not run once this host hands the job to a separate Worker");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BackgroundProcessing__Enabled", null);
        }
    }
}

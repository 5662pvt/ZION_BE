using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
using ZIONShop.EventBus.RabbitMq;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Notifications.NotificationsTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Orders;

[Collection(ApiTestCollection.Name)]
public class PlaceOrderIdempotencyTests
{
    private readonly ApiFactory _factory;

    public PlaceOrderIdempotencyTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> factory)
    {
        var admin = factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    /// <summary>
    /// Flushes one batch of a module's outbox synchronously, standing in for the background
    /// <see cref="OutboxPublisherService{TDbContext}"/> that a BackgroundProcessing-disabled host never
    /// starts -- lets a test control exactly when a pending event is delivered instead of racing an
    /// automatic polling timer. <see cref="OutboxPublisherService{TDbContext}"/> itself is only
    /// registered in DI when background processing is enabled, so it is constructed directly here from
    /// its otherwise-ungated dependencies.
    /// </summary>
    private static async Task PumpOutboxOnceAsync<TDbContext>(WebApplicationFactory<Program> factory) where TDbContext : DbContext
    {
        using var scope = factory.Services.CreateScope();
        var publisher = new OutboxPublisherService<TDbContext>(
            scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(),
            scope.ServiceProvider.GetRequiredService<IOptions<RabbitMqOptions>>(),
            scope.ServiceProvider.GetRequiredService<ILogger<OutboxPublisherService<TDbContext>>>());
        await publisher.PublishBatchAsync(CancellationToken.None);
    }

    /// <summary>
    /// Removes the one still-pending outbox row for <typeparamref name="TEvent"/>/<paramref name="orderId"/>
    /// so it can be stood in for separately (a hand-built "late" event, or simply skipped) without
    /// <see cref="PumpOutboxOnceAsync{TDbContext}"/> picking it up and dispatching it bundled together with
    /// whatever else is pending in the same module's outbox at that moment -- <c>PublishBatchAsync</c>
    /// flushes up to 50 eligible rows per call, not just one, so two events that both happen to be pending
    /// (e.g. OrderCreated and OrderCancelled) would otherwise be delivered in the same batch.
    /// </summary>
    private static async Task DiscardPendingOutboxMessageAsync<TEvent>(WebApplicationFactory<Program> factory, Guid orderId) where TEvent : IntegrationEvent
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null && m.Type.Contains(typeof(TEvent).Name))
            .ToListAsync();
        var match = pending.Single(m => m.Content.Contains(orderId.ToString()));
        db.Set<OutboxMessage>().Remove(match);
        await db.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> PlaceOrderWithKeyAsync(
        HttpClient client, Guid addressId, string paymentMethod, string? idempotencyKey, string? note = null, string? couponCode = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new { addressId, paymentMethod, note, couponCode })
        };
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<(OrderData Order, bool Replayed)> PlaceOrderAndParseAsync(
        HttpClient client, Guid addressId, string paymentMethod, string idempotencyKey, string? note = null)
    {
        var response = await PlaceOrderWithKeyAsync(client, addressId, paymentMethod, idempotencyKey, note);
        response.EnsureSuccessStatusCode();
        var replayed = response.Headers.Contains("Idempotent-Replayed");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
        return (envelope!.Data!, replayed);
    }

    private static async Task<int> GetMyOrdersCountAsync(HttpClient customer)
    {
        var response = await customer.GetAsync("/api/v1/orders/mine?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<object>>>(ApiEnvelopeJson.Options);
        return envelope!.Pagination!.TotalCount;
    }

    [Fact]
    public async Task SameKey_Should_ReturnSameOrder_When_CalledSequentiallyFiveTimes()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var key = $"seq-{Guid.NewGuid():N}";

        var results = new List<(OrderData Order, bool Replayed)>();
        for (var i = 0; i < 5; i++)
            results.Add(await PlaceOrderAndParseAsync(customer, addressId, "COD", key, note: "same body"));

        results.Select(r => r.Order.Id).Distinct().Should().ContainSingle();
        results[0].Replayed.Should().BeFalse();
        results.Skip(1).Should().OnlyContain(r => r.Replayed);
        (await GetMyOrdersCountAsync(customer)).Should().Be(1);
    }

    [Fact]
    public async Task SameKey_Should_CreateExactlyOneOrder_When_CalledConcurrently()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var key = $"con-{Guid.NewGuid():N}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => PlaceOrderWithKeyAsync(customer, addressId, "COD", key, note: "same body")));

        foreach (var response in responses)
            ((int)response.StatusCode).Should().BeInRange(200, 299, $"every concurrent request for the same Idempotency-Key must settle 2xx, got {response.StatusCode}");

        var orderIds = new List<Guid>();
        foreach (var response in responses)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
            orderIds.Add(envelope!.Data!.Id);
        }

        orderIds.Distinct().Should().ContainSingle();
        (await GetMyOrdersCountAsync(customer)).Should().Be(1);
    }

    [Fact]
    public async Task SameKeyWithPerUserLimitedCoupon_Should_CreateExactlyOneOrder_When_CalledConcurrently()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 700000m);
        await AdjustStockAsync(admin, productId, 10);

        var promotion = await CreatePromotionAsync(
            admin, type: "PercentageOff", value: 10m, maxDiscountAmount: 50000m,
            minOrderSubtotal: 300000m, usageLimit: 10, perUserLimit: 1);
        var coupon = await CreateCouponAsync(admin, promotion.Id, "LOCK10PCT");
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var key = $"coupon-con-{Guid.NewGuid():N}";

        var responses = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => PlaceOrderWithKeyAsync(customer, addressId, "COD", key, couponCode: coupon.Code)));

        foreach (var response in responses)
            ((int)response.StatusCode).Should().BeInRange(200, 299, $"every concurrent request for the same Idempotency-Key must settle 2xx, got {response.StatusCode}");

        var orderIds = new List<Guid>();
        foreach (var response in responses)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
            orderIds.Add(envelope!.Data!.Id);
        }

        orderIds.Distinct().Should().ContainSingle();
        (await GetMyOrdersCountAsync(customer)).Should().Be(1);

        var afterAll = await WaitForPromotionUsedCountAsync(admin, promotion.Id, 1);
        afterAll.UsedCount.Should().Be(1);
    }

    [Fact]
    public async Task SameKey_Should_ReturnConflict_When_RequestBodyDiffers()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var key = $"reuse-{Guid.NewGuid():N}";

        var first = await PlaceOrderAndParseAsync(customer, addressId, "COD", key, note: "first note");

        var secondResponse = await PlaceOrderWithKeyAsync(customer, addressId, "COD", key, note: "a different note");

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await secondResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.IdempotencyKeyReused");
        (await GetMyOrdersCountAsync(customer)).Should().Be(1);
    }

    [Fact]
    public async Task NoKey_Should_CreateTwoOrders_When_PlacedTwiceWithoutIdempotencyKey()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);

        await AddToCartAsync(customer, productId, 1);
        var first = await PlaceOrderAsync(customer, addressId, "COD");

        await AddToCartAsync(customer, productId, 1); // refill — first order's cart clear is async via outbox
        var second = await PlaceOrderAsync(customer, addressId, "COD");

        second.Id.Should().NotBe(first.Id);
        (await GetMyOrdersCountAsync(customer)).Should().Be(2);
    }

    [Fact]
    public async Task InvalidKey_Should_Return400_When_KeyExceeds100Chars()
    {
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        var tooLongKey = new string('a', 101);

        var response = await PlaceOrderWithKeyAsync(customer, addressId, "COD", tooLongKey);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidKey_Should_Return400_When_KeyHasDisallowedCharacters()
    {
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await PlaceOrderWithKeyAsync(customer, addressId, "COD", "has a space");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task StockPreCheck_Should_ReturnConflict_When_CartQuantityExceedsAvailable()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 1);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 2);

        var response = await PlaceOrderRawAsync(customer, addressId, "COD");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.InsufficientStock");
        (await GetMyOrdersCountAsync(customer)).Should().Be(0);
    }

    [Fact]
    public async Task AsyncReservationFailure_Should_CancelOrder_And_NotifyCustomer()
    {
        await using var host = await IsolatedBackgroundDisabledHost.CreateAsync();
        var factory = host.Factory;

        var admin = await AdminClientAsync(factory);
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD"); // passes the sync pre-check (5 available >= 1)

        // Simulates the async Inventory reservation failing outright (e.g. a concurrent order depleted stock
        // between the sync pre-check and the real atomic reservation) by publishing the same event Inventory
        // would publish in that case, through the real integration-event pipeline (not by touching Inventory).
        // The real OrderCreated outbox row for this order is discarded first so it can never legitimately
        // reserve/confirm the order and race this synthetic failure -- this host's own
        // OutboxPublisherService<OrdersDbContext> never runs anyway (BackgroundProcessing disabled) and no
        // other host shares this isolated database, so the discard itself races nothing.
        await DiscardPendingOutboxMessageAsync<OrderCreatedIntegrationEvent>(factory, placed.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(new InventoryReservationFailedIntegrationEvent(placed.Id, "Hết hàng", new[] { productId }));
        }

        var cancelled = await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");
        cancelled.CancelReason.Should().Be("Hết hàng");

        // The cancellation above queued OrderCancelledIntegrationEvent as the only pending row left on
        // Orders' own outbox (OrderCreated was discarded) -- flush it ourselves so Notifications picks it
        // up, since no background publisher is running.
        await PumpOutboxOnceAsync<OrdersDbContext>(factory);

        var log = await WaitForNotificationLogAsync(admin, placed.Id, "OrderCancelled");
        log.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task LateReservation_Should_BeReleased_When_ItArrivesAfterOrderWasAlreadyCancelled()
    {
        await using var host = await IsolatedBackgroundDisabledHost.CreateAsync();
        var factory = host.Factory;

        var admin = await AdminClientAsync(factory);
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);
        var (customer, addressId, userId) = await CreateCustomerWithAddressAsync(factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        // Reproduces the F3 leak directly: discard the real OrderCreated outbox row (standing in for it
        // being stuck in a delayed retry queue) so the order gets cancelled while Inventory has reserved
        // nothing for it yet, and so that flushing Orders' outbox next delivers only OrderCancelled -- if
        // OrderCreated were still pending alongside it, PublishBatchAsync would deliver both in the same
        // batch, and Inventory's own (unrelated) OrderCancelledIntegrationEventHandler would release the
        // reservation immediately, short-circuiting the actual leak this test targets. This host's own
        // OutboxPublisherService<OrdersDbContext> never runs (BackgroundProcessing disabled) and no other
        // host shares this isolated database, so the discard races nothing.
        await DiscardPendingOutboxMessageAsync<OrderCreatedIntegrationEvent>(factory, placed.Id);
        var cancelResponse = await CancelOrderRawAsync(customer, placed.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");

        // Only OrderCancelled is pending now -- flush it alone so Inventory's OrderCancelledIntegrationEventHandler
        // runs its (idempotent, no-op) release check before any reservation exists, exactly like production.
        await PumpOutboxOnceAsync<OrdersDbContext>(factory);

        var beforeLateReservation = await GetInventoryItemBySkuAsync(admin, sku);
        beforeLateReservation.Reserved.Should().Be(0);

        // The delayed retry finally lands: Inventory has no idea the order was cancelled and reserves stock
        // for it anyway (COD -> no expiry), publishing the real InventoryReservedIntegrationEvent through its
        // own outbox exactly as it would in production. Published directly (not via Orders' outbox) since
        // the real row was discarded above -- this is the "late" delivery itself, not a race workaround.
        using (var scope = factory.Services.CreateScope())
        {
            var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
            await eventBus.PublishAsync(new OrderCreatedIntegrationEvent(
                placed.Id, userId, placed.Total,
                placed.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice, l.IsGift)).ToList(),
                placed.PaymentMethod, placed.Currency));
        }

        // Confirms the reservation really happened (i.e. this test exercises the leak, not a trivial no-op).
        var reservedSnapshot = await WaitForInventoryAsync(admin, sku, i => i.Reserved > 0, TimeSpan.FromSeconds(10));
        reservedSnapshot.Reserved.Should().Be(1);

        // Without the release-on-cancel fix this would stay reserved forever: InventoryReservedIntegrationEventHandler
        // would see the order is Cancelled and silently no-op. With the fix it requests a release, which Inventory's
        // outbox round-trip (InventoryReserved -> Orders -> OrderReservationReleaseRequested -> Inventory) applies --
        // driven explicitly here via PumpOutboxOnceAsync instead of a background timer, since Inventory's and
        // Orders' outboxes each hold exactly one pending row at their respective hop.
        await PumpOutboxOnceAsync<InventoryDbContext>(factory);
        await PumpOutboxOnceAsync<OrdersDbContext>(factory);

        var released = await WaitForInventoryAsync(admin, sku, i => i.Reserved == 0, TimeSpan.FromSeconds(10));
        released.OnHand.Should().Be(5);
        released.Available.Should().Be(5);
    }

    /// <summary>
    /// A dedicated Postgres container (not the shared ApiTestCollection fixture's) with
    /// BackgroundProcessing disabled. The shared fixture's own OutboxPublisherService&lt;TDbContext&gt;
    /// instances, once started, keep polling for the lifetime of the whole suite -- disabling
    /// registration on a second <c>WithWebHostBuilder</c> host pointed at that same database does not
    /// stop them, so a test that needs a real pending outbox row to stay put must not share that
    /// database at all. Same shape as <c>WorkerReadinessIsolationTests.cs</c>, minus RabbitMq since this
    /// scenario only needs EventBus:Provider=InMemory.
    /// </summary>
    private sealed class IsolatedBackgroundDisabledHost : IAsyncDisposable
    {
        private readonly PostgreSqlContainer _postgres;
        private readonly string _uploadsRoot;
        private readonly EnvironmentVariableScope _envScope;

        public WebApplicationFactory<Program> Factory { get; }

        private IsolatedBackgroundDisabledHost(
            PostgreSqlContainer postgres, string uploadsRoot, EnvironmentVariableScope envScope, WebApplicationFactory<Program> factory)
        {
            _postgres = postgres;
            _uploadsRoot = uploadsRoot;
            _envScope = envScope;
            Factory = factory;
        }

        public static async Task<IsolatedBackgroundDisabledHost> CreateAsync()
        {
            var postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("zionshop_outbox_isolation_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();
            await postgres.StartAsync();

            var uploadsRoot = Path.Combine(Path.GetTempPath(), "zion-uploads-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(uploadsRoot);

            // Restored to the shared ApiFactory fixture's own values on Dispose (not null) -- ApiFactory
            // sets these exact variables once, lazily, the first time its host is built; nulling them
            // out here instead would permanently break any later test in the same process (e.g.
            // NpgsqlExportFilterTests) that reads them directly.
            var envScope = new EnvironmentVariableScope(
                "ConnectionStrings__DefaultConnection", "EventBus__Provider", "BackgroundProcessing__Enabled",
                "ImageStorage__PublicBaseUrl", "ImageStorage__LocalRootPath");

            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", postgres.GetConnectionString());
            Environment.SetEnvironmentVariable("EventBus__Provider", "InMemory");
            Environment.SetEnvironmentVariable("BackgroundProcessing__Enabled", "false");
            Environment.SetEnvironmentVariable("ImageStorage__PublicBaseUrl", "http://test.local");
            Environment.SetEnvironmentVariable("ImageStorage__LocalRootPath", uploadsRoot);

            var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

            return new IsolatedBackgroundDisabledHost(postgres, uploadsRoot, envScope, factory);
        }

        public async ValueTask DisposeAsync()
        {
            Factory.Dispose();
            _envScope.Dispose();
            if (Directory.Exists(_uploadsRoot))
                Directory.Delete(_uploadsRoot, recursive: true);
            await _postgres.DisposeAsync();
        }
    }
}

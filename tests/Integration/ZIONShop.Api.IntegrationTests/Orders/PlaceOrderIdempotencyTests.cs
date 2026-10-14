using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
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
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD"); // passes the sync pre-check (5 available >= 1)

        // Simulates the async Inventory reservation failing outright (e.g. a concurrent order depleted stock
        // between the sync pre-check and the real atomic reservation) by publishing the same event Inventory
        // would publish in that case, through the real integration-event pipeline (not by touching Inventory).
        // The real OrderCreated outbox row for this order is discarded first so the still-available stock can
        // never legitimately get reserved and confirm the order in the background, racing this synthetic
        // failure and making the test flaky.
        await DiscardPendingOutboxMessageAsync<OrderCreatedIntegrationEvent>(placed.Id);

        using var scope = _factory.Services.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new InventoryReservationFailedIntegrationEvent(placed.Id, "Hết hàng", new[] { productId }));

        var cancelled = await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");
        cancelled.CancelReason.Should().Be("Hết hàng");

        var log = await WaitForNotificationLogAsync(admin, placed.Id, "OrderCancelled");
        log.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task LateReservation_Should_BeReleased_When_ItArrivesAfterOrderWasAlreadyCancelled()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);
        var (customer, addressId, userId) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        // Reproduces the F3 leak directly: discard the real OrderCreated outbox row (standing in for it
        // being stuck in a delayed retry queue) and cancel the order while Inventory has not reserved
        // anything for it yet.
        await DiscardPendingOutboxMessageAsync<OrderCreatedIntegrationEvent>(placed.Id);
        var cancelResponse = await CancelOrderRawAsync(customer, placed.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");

        var beforeLateReservation = await GetInventoryItemBySkuAsync(admin, sku);
        beforeLateReservation.Reserved.Should().Be(0);

        // The delayed retry finally lands: Inventory has no idea the order was cancelled and reserves stock
        // for it anyway (COD -> no expiry), publishing the real InventoryReservedIntegrationEvent through its
        // own outbox exactly as it would in production.
        using var scope = _factory.Services.CreateScope();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new OrderCreatedIntegrationEvent(
            placed.Id, userId, placed.Total,
            placed.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice, l.IsGift)).ToList(),
            placed.PaymentMethod, placed.Currency));

        // Confirms the reservation really happened (i.e. this test exercises the leak, not a trivial no-op).
        var reservedSnapshot = await WaitForInventoryAsync(admin, sku, i => i.Reserved > 0, TimeSpan.FromSeconds(10));
        reservedSnapshot.Reserved.Should().Be(1);

        // Without the release-on-cancel fix this would stay reserved forever: InventoryReservedIntegrationEventHandler
        // would see the order is Cancelled and silently no-op. With the fix it requests a release, which Inventory's
        // outbox round-trip (InventoryReserved -> Orders -> OrderReservationReleaseRequested -> Inventory) applies
        // asynchronously, so Reserved must return to its starting value.
        var released = await WaitForInventoryAsync(admin, sku, i => i.Reserved == 0, TimeSpan.FromSeconds(30));
        released.OnHand.Should().Be(5);
        released.Available.Should().Be(5);
    }

    private async Task DiscardPendingOutboxMessageAsync<TEvent>(Guid orderId) where TEvent : IntegrationEvent
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null && m.Type.Contains(typeof(TEvent).Name))
            .ToListAsync();
        var match = pending.Single(m => m.Content.Contains(orderId.ToString()));
        db.Set<OutboxMessage>().Remove(match);
        await db.SaveChangesAsync();
    }
}

using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Loyalty;

public record OrderPointsData(Guid Id, string Code, string Status, decimal Subtotal, decimal ShippingFee, decimal DiscountAmount, decimal Total, int PointsUsed, decimal PointsDiscount);

[Collection(ApiTestCollection.Name)]
public class CheckoutWithPointsTests
{
    private readonly ApiFactory _factory;

    public CheckoutWithPointsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task AdminAdjustLoyaltyAsync(HttpClient admin, Guid userId, int delta, string reason = "Integration test seed")
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/admin/loyalty/users/{userId}/adjust", new { delta, reason });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<LoyaltySummaryData> GetMyLoyaltyAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/loyalty/me");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoyaltySummaryData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<int> WaitForLoyaltyBalanceAsync(HttpClient client, Func<int, bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        LoyaltySummaryData last;
        do
        {
            last = await GetMyLoyaltyAsync(client);
            if (predicate(last.Balance)) return last.Balance;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Loyalty balance did not reach the expected state within {timeout}. Last observed: {last.Balance}.");
    }

    private static async Task<OrderPointsData> PlaceOrderWithPointsAsync(HttpClient client, Guid addressId, string paymentMethod, bool usePoints)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { addressId, paymentMethod, usePoints });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderPointsData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<OrderPointsData> GetOrderAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderPointsData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<OrderPointsData> WaitForOrderStatusAsync(HttpClient client, Guid orderId, string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        OrderPointsData last;
        do
        {
            last = await GetOrderAsync(client, orderId);
            if (last.Status == expectedStatus) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Order {orderId} did not reach status '{expectedStatus}' within {timeout}. Last observed status: '{last.Status}'.");
    }

    private static async Task CancelOrderAsync(HttpClient client, Guid orderId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { reason = (string?)null });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PlaceOrder_Should_ApplyPointsAndReduceBalance_When_UsePointsTrueAndBalanceBelowCap()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 1_000_000m);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, userId) = await CreateCustomerWithAddressAsync(_factory);
        await AdminAdjustLoyaltyAsync(admin, userId, 100_000, "seed");
        await AddToCartAsync(customer, productId, 1);

        var order = await PlaceOrderWithPointsAsync(customer, addressId, "COD", usePoints: true);

        order.PointsUsed.Should().Be(100_000); // balance (100000) < 50% of subtotal (500000)
        order.PointsDiscount.Should().Be(100_000m); // 1 xu = 1 đ
        order.Total.Should().Be(order.Subtotal - order.DiscountAmount - order.PointsDiscount + order.ShippingFee);

        var balance = await WaitForLoyaltyBalanceAsync(customer, b => b == 0);
        balance.Should().Be(0);
    }

    [Fact]
    public async Task CancelOrder_Should_RestoreBalance_When_OrderUsedPoints()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 1_000_000m);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, userId) = await CreateCustomerWithAddressAsync(_factory);
        await AdminAdjustLoyaltyAsync(admin, userId, 100_000, "seed");
        await AddToCartAsync(customer, productId, 1);

        var order = await PlaceOrderWithPointsAsync(customer, addressId, "COD", usePoints: true);
        order.PointsUsed.Should().Be(100_000);
        await WaitForLoyaltyBalanceAsync(customer, b => b == 0);

        await CancelOrderAsync(customer, order.Id);

        var restored = await WaitForLoyaltyBalanceAsync(customer, b => b == 100_000);
        restored.Should().Be(100_000);
    }

    [Fact]
    public async Task CompleteOrder_Should_EarnBasedOnSubtotalMinusPointsDiscount_When_OrderUsedPoints()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 1_000_000m);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, userId) = await CreateCustomerWithAddressAsync(_factory);
        await AdminAdjustLoyaltyAsync(admin, userId, 100_000, "seed");
        await AddToCartAsync(customer, productId, 1);

        var order = await PlaceOrderWithPointsAsync(customer, addressId, "COD", usePoints: true);
        order.PointsUsed.Should().Be(100_000);
        await WaitForOrderStatusAsync(customer, order.Id, "Confirmed");

        (await admin.PostAsync($"/api/v1/orders/{order.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{order.Id}/complete", null)).EnsureSuccessStatusCode();
        await WaitForOrderStatusAsync(customer, order.Id, "Completed");

        var expectedEarn = (int)Math.Floor((order.Subtotal - order.PointsDiscount) * 1 / 100m); // EarnRatePercent = 1
        var balance = await WaitForLoyaltyBalanceAsync(customer, b => b == expectedEarn);
        balance.Should().Be(expectedEarn);
    }

    [Fact]
    public async Task PlaceOrder_Should_PlaceWithoutPoints_When_UsePointsTrueButBalanceIsZero()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100_000m);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var order = await PlaceOrderWithPointsAsync(customer, addressId, "COD", usePoints: true);

        order.PointsUsed.Should().Be(0);
        order.PointsDiscount.Should().Be(0m);
        order.Total.Should().Be(order.Subtotal - order.DiscountAmount + order.ShippingFee);
    }
}

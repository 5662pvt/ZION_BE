using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Notifications;

public record MyNotificationData(Guid Id, string Type, string Subject, Guid OrderId, string Status, DateTime CreatedAt, DateTime? SentAt);

[Collection(ApiTestCollection.Name)]
public class MyNotificationsTests
{
    private readonly ApiFactory _factory;

    public MyNotificationsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<List<MyNotificationData>> GetMineAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/notifications/mine");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<MyNotificationData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<MyNotificationData> WaitForMineAsync(HttpClient client, Guid orderId, string type, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            var mine = await GetMineAsync(client);
            var match = mine.FirstOrDefault(l => l.OrderId == orderId && l.Type == type);
            if (match is not null) return match;
            await Task.Delay(500);
        }

        throw new TimeoutException($"No 'mine' notification of type '{type}' appeared for order {orderId} within {timeout}.");
    }

    [Fact]
    public async Task GetMine_Should_ShowOrderConfirmed_When_CustomerCodOrderIsConfirmed()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var mine = await WaitForMineAsync(customer, placed.Id, "OrderConfirmed");
        mine.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task GetMine_Should_NotShowAnotherCustomersNotifications()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customerA, addressIdA, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customerA, productId, 1);
        var placedA = await PlaceOrderAsync(customerA, addressIdA, "COD");
        await WaitForOrderStatusAsync(customerA, placedA.Id, "Confirmed");
        await WaitForMineAsync(customerA, placedA.Id, "OrderConfirmed");

        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var mineForB = await GetMineAsync(customerB);

        mineForB.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMine_Should_ReturnUnauthorized_When_Anonymous()
    {
        var anonymous = _factory.CreateClient();
        ((int)(await anonymous.GetAsync("/api/v1/notifications/mine")).StatusCode).Should().Be(401);
    }
}

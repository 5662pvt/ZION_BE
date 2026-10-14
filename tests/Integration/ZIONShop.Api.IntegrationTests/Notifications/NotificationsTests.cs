using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Notifications.NotificationsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Notifications;

[Collection(ApiTestCollection.Name)]
public class NotificationsTests
{
    private readonly ApiFactory _factory;

    public NotificationsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(HttpClient Admin, Guid OrderId, HttpClient Customer)> PlaceConfirmedCodOrderAsync()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        return (admin, placed.Id, customer);
    }

    [Fact]
    public async Task OrderConfirmed_Should_LogSentEmailExactlyOnce_When_CodOrderIsConfirmed()
    {
        var (admin, orderId, _) = await PlaceConfirmedCodOrderAsync();

        var log = await WaitForNotificationLogAsync(admin, orderId, "OrderConfirmed");
        log.Status.Should().Be("Sent");

        var logs = await GetNotificationLogsAsync(admin, orderId);
        logs.Count(l => l.Type == "OrderConfirmed").Should().Be(1);
    }

    [Fact]
    public async Task OrderShipped_Should_LogSentEmail_When_AdminShipsConfirmedOrder()
    {
        var (admin, orderId, _) = await PlaceConfirmedCodOrderAsync();

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{orderId}/ship", null);
        shipResponse.EnsureSuccessStatusCode();

        var log = await WaitForNotificationLogAsync(admin, orderId, "OrderShipped");
        log.Status.Should().Be("Sent");
    }

    [Fact]
    public async Task OrderCancelled_Should_LogSentEmail_When_CustomerCancelsConfirmedOrder()
    {
        var (admin, orderId, customer) = await PlaceConfirmedCodOrderAsync();

        var cancelResponse = await CancelOrderRawAsync(customer, orderId, "Đổi ý không mua nữa");
        cancelResponse.EnsureSuccessStatusCode();

        var log = await WaitForNotificationLogAsync(admin, orderId, "OrderCancelled");
        log.Status.Should().Be("Sent");
    }
}

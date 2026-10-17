using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Notifications;

public record UserNotificationFeedData(Guid Id, string Type, string Title, string Message, Guid? OrderId, bool IsRead, DateTime CreatedAt);
public record UnreadCountData(int Count);

[Collection(ApiTestCollection.Name)]
public class NotificationsFeedTests
{
    private readonly ApiFactory _factory;

    public NotificationsFeedTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<List<UserNotificationFeedData>> GetFeedAsync(HttpClient client, bool unreadOnly = false)
    {
        var response = await client.GetAsync($"/api/v1/notifications/feed?unreadOnly={unreadOnly}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<UserNotificationFeedData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<int> GetUnreadCountAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/notifications/feed/unread-count");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UnreadCountData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!.Count;
    }

    private static async Task<UserNotificationFeedData> WaitForFeedEntryAsync(HttpClient client, Guid orderId, string type, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            var feed = await GetFeedAsync(client);
            var match = feed.FirstOrDefault(n => n.OrderId == orderId && n.Type == type);
            if (match is not null) return match;
            await Task.Delay(500);
        }

        throw new TimeoutException($"No feed entry of type '{type}' appeared for order {orderId} within {timeout}.");
    }

    [Fact]
    public async Task GetFeed_Should_ShowOrderPlaced_When_CustomerPlacesOrder()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        var entry = await WaitForFeedEntryAsync(customer, placed.Id, "OrderPlaced");
        entry.IsRead.Should().BeFalse();
        entry.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnreadCount_Should_IncrementAndThenDecrement_AfterMarkRead()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        var entry = await WaitForFeedEntryAsync(customer, placed.Id, "OrderPlaced");

        var countBefore = await GetUnreadCountAsync(customer);
        countBefore.Should().BeGreaterThanOrEqualTo(1);

        var markResponse = await customer.PostAsync($"/api/v1/notifications/feed/{entry.Id}/read", null);
        markResponse.EnsureSuccessStatusCode();

        var countAfter = await GetUnreadCountAsync(customer);
        countAfter.Should().Be(countBefore - 1);
    }

    [Fact]
    public async Task MarkRead_Should_ReturnNotFound_When_NotificationBelongsToAnotherUser()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customerA, addressIdA, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customerA, productId, 1);
        var placedA = await PlaceOrderAsync(customerA, addressIdA, "COD");
        var entryA = await WaitForFeedEntryAsync(customerA, placedA.Id, "OrderPlaced");

        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var response = await customerB.PostAsync($"/api/v1/notifications/feed/{entryA.Id}/read", null);

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task MarkAllRead_Should_MarkEveryUnreadNotificationForCaller()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForFeedEntryAsync(customer, placed.Id, "OrderPlaced");

        var markAllResponse = await customer.PostAsync("/api/v1/notifications/feed/read-all", null);
        markAllResponse.EnsureSuccessStatusCode();

        var unreadAfter = await GetFeedAsync(customer, unreadOnly: true);
        unreadAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task GetFeed_Should_ReturnUnauthorized_When_Anonymous()
    {
        var anonymous = _factory.CreateClient();
        ((int)(await anonymous.GetAsync("/api/v1/notifications/feed")).StatusCode).Should().Be(401);
    }

    [Fact]
    public async Task GetFeed_Should_ShowOrderCreatedForReview_When_CustomerPlacesOrder()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        var entry = await WaitForFeedEntryAsync(admin, placed.Id, "OrderCreatedForReview");
        entry.IsRead.Should().BeFalse();
        entry.Title.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetFeed_Should_ShowShippedThenCompleted_When_AdminShipsThenCompletesOrder()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        await WaitForFeedEntryAsync(admin, placed.Id, "OrderCreatedForReview");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null);
        shipResponse.EnsureSuccessStatusCode();
        await WaitForFeedEntryAsync(customer, placed.Id, "OrderShipped");

        var completeResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();
        await WaitForFeedEntryAsync(customer, placed.Id, "OrderCompleted");
    }
}

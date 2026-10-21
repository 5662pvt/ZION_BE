using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Returns;

public record ReturnFeedEntryData(Guid Id, string Type, string Title, string Message, Guid? OrderId, bool IsRead, DateTime CreatedAt);

/// <summary>
/// Covers the W20-BE-W1b slice of the return lifecycle: Inventory restocking the committed reservation
/// once a return is Received, and the Notifications fan-out/targeted entries raised along the way.
/// New file -- <see cref="ReturnsFlowTests"/> (W0) already covers the Orders-side status machine and is
/// left untouched.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ReturnsInventoryAndNotificationsTests
{
    private readonly ApiFactory _factory;

    public ReturnsInventoryAndNotificationsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<OrderData> PlaceShipAndCompleteOrderAsync(HttpClient admin, HttpClient customer, Guid addressId)
    {
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null)).EnsureSuccessStatusCode();

        return await GetOrderAsync(customer, placed.Id);
    }

    private static async Task<List<ReturnFeedEntryData>> GetFeedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/notifications/feed");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ReturnFeedEntryData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<ReturnFeedEntryData> WaitForFeedEntryAsync(HttpClient client, Guid orderId, string type, TimeSpan? timeout = null)
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
    public async Task FullReturnLifecycle_Should_RestockInventory_And_NotifyAdminAndCustomer()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin, price: 120000m);
        await AdjustStockAsync(admin, productId, 10);
        var onHandBeforeOrder = (await GetInventoryItemBySkuAsync(admin, sku)).OnHand;

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 3);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId);
        completed.Status.Should().Be("Completed");

        // COD commits the reservation (OnHand -= quantity) as soon as the order confirms.
        var onHandAfterCommit = await WaitForInventoryAsync(admin, sku, i => i.OnHand == onHandBeforeOrder - 3);
        onHandAfterCommit.OnHand.Should().Be(onHandBeforeOrder - 3);

        var requested = await RequestReturnAsync(customer, completed.Id, "Defective", "Sản phẩm bị lỗi");
        var adminRequestedEntry = await WaitForFeedEntryAsync(admin, completed.Id, "ReturnRequested");
        adminRequestedEntry.Message.Should().Contain("Defective");

        await AdminApproveReturnAsync(admin, requested.Id);
        var customerApprovedEntry = await WaitForFeedEntryAsync(customer, completed.Id, "ReturnApproved");
        customerApprovedEntry.IsRead.Should().BeFalse();

        await MarkShippedBackAsync(customer, requested.Id, "VN-TRACK-99");
        await WaitForFeedEntryAsync(admin, completed.Id, "ReturnShippedBack");

        await AdminMarkReceivedAsync(admin, requested.Id);

        // Inventory's OrderReturnReceivedIntegrationEventHandler restocks the committed reservation once
        // the outbox delivers the event -- poll rather than read once (same rationale as WaitForInventoryAsync).
        var restocked = await WaitForInventoryAsync(admin, sku, i => i.OnHand == onHandBeforeOrder, TimeSpan.FromSeconds(30));
        restocked.OnHand.Should().Be(onHandBeforeOrder);

        // Payments' own OrderReturnReceivedIntegrationEventHandler refunds COD returns immediately and
        // queues OrderRefundedIntegrationEvent as soon as it processes the same Received event above --
        // no manual simulation needed here, just wait for that async chain to reach the customer feed.
        var customerRefundedEntry = await WaitForFeedEntryAsync(customer, completed.Id, "ReturnRefunded", TimeSpan.FromSeconds(30));
        customerRefundedEntry.Message.Should().Contain("tiền mặt");

        var returnedOrder = await GetOrderAsync(customer, completed.Id);
        returnedOrder.Status.Should().Be("Returned");
        returnedOrder.ReturnRequest!.Status.Should().Be("Refunded");
    }
}

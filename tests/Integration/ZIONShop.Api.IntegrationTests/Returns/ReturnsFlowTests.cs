using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Returns;

[Collection(ApiTestCollection.Name)]
public class ReturnsFlowTests
{
    private readonly ApiFactory _factory;

    public ReturnsFlowTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<OrderData> PlaceShipAndCompleteOrderAsync(HttpClient admin, HttpClient customer, Guid addressId, Guid productId)
    {
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null)).EnsureSuccessStatusCode();

        return await GetOrderAsync(customer, placed.Id);
    }

    /// <summary>The Received -> Refunded transition is now driven by the real outbox chain
    /// (Payments' <c>OrderReturnReceivedIntegrationEventHandler</c> refunds automatically and publishes
    /// <see cref="OrderRefundedIntegrationEvent"/> via its own outbox), which settles asynchronously
    /// after the Received call returns -- same rationale as <see cref="CheckoutTestHelpers.WaitForOrderStatusAsync"/>.</summary>
    private static async Task<ReturnRequestData> WaitForReturnStatusAsync(HttpClient client, Guid orderId, string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        ReturnRequestData? last = null;
        do
        {
            last = await GetReturnAsync(client, orderId);
            if (last?.Status == expectedStatus) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Return request for order {orderId} did not reach status '{expectedStatus}' within {timeout}. Last observed status: '{last?.Status}'.");
    }

    [Fact]
    public async Task FullFlow_Should_GoFromRequested_To_Refunded_And_OrderReturned()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId, productId);
        completed.Status.Should().Be("Completed");

        var requested = await RequestReturnAsync(customer, completed.Id, "Defective", "Sản phẩm bị lỗi");
        requested.Status.Should().Be("Requested");
        requested.RefundAmount.Should().Be(completed.Total);

        var fetchedLatest = await GetReturnAsync(customer, completed.Id);
        fetchedLatest!.Id.Should().Be(requested.Id);

        var approved = await AdminApproveReturnAsync(admin, requested.Id);
        approved.Status.Should().Be("Approved");

        var shippedBack = await MarkShippedBackAsync(customer, requested.Id, "VN-TRACK-1");
        shippedBack.Status.Should().Be("ShippedBack");
        shippedBack.ReturnTrackingCode.Should().Be("VN-TRACK-1");

        var received = await AdminMarkReceivedAsync(admin, requested.Id);
        received.Status.Should().Be("Received");

        // The outbox event carrying the refund instructions for Payments/Inventory (next wave) must have
        // been written at the Received transition.
        using (var verifyScope = _factory.Services.CreateScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var outboxTypes = await db.Set<OutboxMessage>()
                .Where(m => m.Type.Contains(nameof(OrderReturnReceivedIntegrationEvent)))
                .Select(m => m.Content)
                .ToListAsync();
            outboxTypes.Should().ContainSingle(c => c.Contains(requested.Id.ToString()) && c.Contains(productId.ToString()));
        }

        var refundedReturn = await WaitForReturnStatusAsync(customer, completed.Id, "Refunded");
        refundedReturn.RefundMethod.Should().Be("COD");

        var returnedOrder = await GetOrderAsync(customer, completed.Id);
        returnedOrder.Status.Should().Be("Returned");
        returnedOrder.ReturnRequest!.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task RevenueReport_Should_Exclude_ReturnedOrder()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 150000m);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId, productId);

        var requested = await RequestReturnAsync(customer, completed.Id, "Other", null);
        await AdminApproveReturnAsync(admin, requested.Id);
        await AdminMarkReceivedAsync(admin, requested.Id);
        await WaitForReturnStatusAsync(customer, completed.Id, "Refunded");

        (await GetOrderAsync(customer, completed.Id)).Status.Should().Be("Returned");

        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOrderReportReader>();
        var now = DateTime.UtcNow;
        var rows = await reader.GetCompletedOrdersAsync(now.AddMinutes(-10), now.AddMinutes(10));

        rows.Should().NotContain(r => r.OrderId == completed.Id);
    }

    [Fact]
    public async Task RequestReturn_Should_Return404_When_NonOwnerRequests()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customerA, addressA, _) = await CreateCustomerWithAddressAsync(_factory);
        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customerA, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customerA, addressA, productId);

        var response = await RequestReturnRawAsync(customerB, completed.Id, "Other", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RequestReturn_Should_Return409_When_OutsideWindow()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId, productId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var order = await db.Orders.Include(o => o.StatusHistory).SingleAsync(o => o.Id == completed.Id);
            var completedHistory = order.StatusHistory.Single(h => h.Status == OrderStatus.Completed);
            db.Entry(completedHistory).Property(h => h.At).CurrentValue = DateTime.UtcNow.AddDays(-10);
            await db.SaveChangesAsync();
        }

        var response = await RequestReturnRawAsync(customer, completed.Id, "Other", null);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.ReturnWindowExpired");
    }

    [Fact]
    public async Task RequestReturn_Should_Return409_When_ActiveRequestAlreadyExists()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId, productId);

        await RequestReturnAsync(customer, completed.Id, "Defective", null);
        var secondResponse = await RequestReturnRawAsync(customer, completed.Id, "Other", null);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await secondResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.ReturnAlreadyRequested");
    }

    [Fact]
    public async Task RejectedReturn_Should_AllowNewRequest_And_AdminReject_Should_QueueDecidedEvent()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId, productId);

        var firstRequest = await RequestReturnAsync(customer, completed.Id, "ChangedMind", null);
        var rejectResponse = await AdminRejectReturnRawAsync(admin, firstRequest.Id, "Không đủ điều kiện trả hàng");
        rejectResponse.EnsureSuccessStatusCode();

        var secondRequest = await RequestReturnAsync(customer, completed.Id, "Defective", "Lần hai");
        secondRequest.Id.Should().NotBe(firstRequest.Id);
        secondRequest.Status.Should().Be("Requested");
    }

    [Fact]
    public async Task GetReturn_Should_ReturnNullData_When_OrderHasNoReturnRequestYet()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOrderAsync(admin, customer, addressId, productId);

        var response = await GetReturnRawAsync(customer, completed.Id);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Data.Should().BeNull();
    }
}

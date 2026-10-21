using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Payments.PaymentsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Returns;

/// <summary>
/// End-to-end: whole-order return reaching Received drives the real outbox chain Orders -> Payments
/// (automatic simulated refund) -> Orders, with no handler invoked directly (unlike <see cref="ReturnsFlowTests"/>'s
/// FullFlow test, written before the Payments side existed). Covers both COD (no Payment row ever
/// projected for this module) and Online (refund against the order's Succeeded Payment).
/// </summary>
[Collection(ApiTestCollection.Name)]
public class PaymentsRefundFlowTests
{
    private readonly ApiFactory _factory;

    public PaymentsRefundFlowTests(ApiFactory factory) => _factory = factory;

    private record RefundData(
        Guid Id, Guid OrderId, Guid ReturnRequestId, Guid? PaymentId, decimal Amount, string Currency,
        string Method, string Status, string? ProviderRefundId, string? FailureReason, DateTime CreatedAt, DateTime? CompletedAt);

    private record AdminPaymentData(
        Guid Id, Guid OrderId, Guid UserId, decimal Amount, string Currency, string Provider, string Channel,
        string Status, string? FailureReason, DateTime CreatedAt, DateTime? CompletedAt, DateTime ExpiresAt);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    /// <summary>Same rationale as <see cref="CheckoutTestHelpers.WaitForOrderStatusAsync"/>: the Received ->
    /// Refunded transition only settles once Orders -> Payments -> Orders have each processed their outbox
    /// message, which happens asynchronously after the Received call returns.</summary>
    private static async Task<ReturnRequestData> WaitForReturnStatusAsync(HttpClient client, Guid orderId, string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        ReturnRequestData? last = null;
        do
        {
            last = await GetReturnAsync(client, orderId);
            if (last?.Status == expectedStatus) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Return request for order {orderId} did not reach status '{expectedStatus}' within {timeout}. Last observed status: '{last?.Status}'.");
    }

    private async Task<RefundData?> GetRefundByReturnRequestIdAsync(HttpClient admin, Guid returnRequestId)
    {
        var response = await admin.GetAsync("/api/v1/admin/refunds?pageSize=100");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<RefundData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!.SingleOrDefault(r => r.ReturnRequestId == returnRequestId);
    }

    private async Task<OrderData> PlaceShipAndCompleteCodOrderAsync(HttpClient admin, HttpClient customer, Guid addressId)
    {
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null)).EnsureSuccessStatusCode();

        return await GetOrderAsync(customer, placed.Id);
    }

    private async Task<OrderData> PlaceShipAndCompleteOnlineOrderAsync(HttpClient admin, HttpClient customer, Guid addressId)
    {
        var placed = await PlaceOrderAsync(customer, addressId, "Online");
        await WaitForOrderStatusAsync(customer, placed.Id, "AwaitingPayment");

        var intent = await CreatePaymentIntentAsync(customer, placed.Id, Guid.NewGuid().ToString("N"));
        await SimulateFakePaymentAsync(customer, intent.IntentId, "success");
        await WaitForOrderStatusAsync(customer, placed.Id, "Paid");

        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null)).EnsureSuccessStatusCode();

        return await GetOrderAsync(customer, placed.Id);
    }

    [Fact]
    public async Task CodOrder_Should_BeAutoRefunded_When_ReturnReceived()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 120000m);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteCodOrderAsync(admin, customer, addressId);

        var requested = await RequestReturnAsync(customer, completed.Id, "Defective", "Hỏng khi mở hộp");
        await AdminApproveReturnAsync(admin, requested.Id);
        await MarkShippedBackAsync(customer, requested.Id, "VN-TRACK-COD");
        await AdminMarkReceivedAsync(admin, requested.Id);

        var refunded = await WaitForReturnStatusAsync(customer, completed.Id, "Refunded");
        refunded.RefundMethod.Should().Be("COD");

        var returnedOrder = await GetOrderAsync(customer, completed.Id);
        returnedOrder.Status.Should().Be("Returned");

        var refund = await GetRefundByReturnRequestIdAsync(admin, requested.Id);
        refund.Should().NotBeNull();
        refund!.Status.Should().Be("Succeeded");
        refund.Method.Should().Be("COD");
        refund.Amount.Should().Be(completed.Total);
        refund.PaymentId.Should().BeNull();
        refund.ProviderRefundId.Should().StartWith("cod_");
    }

    [Fact]
    public async Task OnlineOrder_Should_BeAutoRefunded_And_MarkPaymentRefunded_When_ReturnReceived()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 200000m);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var completed = await PlaceShipAndCompleteOnlineOrderAsync(admin, customer, addressId);

        var requested = await RequestReturnAsync(customer, completed.Id, "Defective", "Lỗi sản phẩm");
        await AdminApproveReturnAsync(admin, requested.Id);
        await MarkShippedBackAsync(customer, requested.Id, "VN-TRACK-ONLINE");
        await AdminMarkReceivedAsync(admin, requested.Id);

        var refunded = await WaitForReturnStatusAsync(customer, completed.Id, "Refunded");
        refunded.RefundMethod.Should().Be("Online");

        var returnedOrder = await GetOrderAsync(customer, completed.Id);
        returnedOrder.Status.Should().Be("Returned");

        var refund = await GetRefundByReturnRequestIdAsync(admin, requested.Id);
        refund.Should().NotBeNull();
        refund!.Status.Should().Be("Succeeded");
        refund.Method.Should().Be("Online");
        refund.Amount.Should().Be(completed.Total);
        refund.PaymentId.Should().NotBeNull();
        refund.ProviderRefundId.Should().StartWith("fakerefund_");

        var paymentsResponse = await admin.GetAsync($"/api/v1/admin/payments?orderId={completed.Id}");
        paymentsResponse.EnsureSuccessStatusCode();
        var paymentsEnvelope = await paymentsResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<AdminPaymentData>>>(ApiEnvelopeJson.Options);
        var payment = paymentsEnvelope!.Data!.Should().ContainSingle().Subject;
        payment.Status.Should().Be("Refunded");
    }
}

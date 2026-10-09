using System.Net;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Payments.PaymentsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Payments;

[Collection(ApiTestCollection.Name)]
public class PaymentsFlowTests
{
    private readonly ApiFactory _factory;

    public PaymentsFlowTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(HttpClient Customer, Guid AddressId, Guid ProductId, string Sku, OrderData Order)> PlaceOnlineOrderAsync(HttpClient admin, int stock = 10)
    {
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, stock);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "Online");
        await WaitForOrderStatusAsync(customer, placed.Id, "AwaitingPayment");

        return (customer, addressId, productId, sku, placed);
    }

    [Fact]
    public async Task CreateIntent_Should_ReturnSameIntent_When_SameIdempotencyKeyReused()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _, _, order) = await PlaceOnlineOrderAsync(admin);
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var first = await CreatePaymentIntentAsync(customer, order.Id, idempotencyKey);
        var second = await CreatePaymentIntentAsync(customer, order.Id, idempotencyKey);

        second.IntentId.Should().Be(first.IntentId);
        first.OrderId.Should().Be(order.Id);
        first.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task SimulateSuccess_Should_MarkOrderPaid_And_CommitStock()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _, sku, order) = await PlaceOnlineOrderAsync(admin);

        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));
        var simulated = await SimulateFakePaymentAsync(customer, intent.IntentId, "success");
        simulated.Status.Should().Be("Succeeded");

        await WaitForOrderStatusAsync(customer, order.Id, "Paid");

        var item = await WaitForInventoryAsync(admin, sku, i => i.OnHand == 9 && i.Reserved == 0);
        item.OnHand.Should().Be(9);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task SimulateFailure_Should_CancelOrder_And_ReleaseStock()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _, sku, order) = await PlaceOnlineOrderAsync(admin);

        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));
        var simulated = await SimulateFakePaymentAsync(customer, intent.IntentId, "failure");
        simulated.Status.Should().Be("Failed");

        await WaitForOrderStatusAsync(customer, order.Id, "Cancelled");

        var item = await WaitForInventoryAsync(admin, sku, i => i.OnHand == 10 && i.Reserved == 0);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task Webhook_Should_Return401_When_SignatureIsInvalid()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _, _, order) = await PlaceOnlineOrderAsync(admin);
        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));

        var anonymous = _factory.CreateClient();
        var response = await PostFakeWebhookRawAsync(anonymous, Guid.NewGuid(), intent.IntentId, "succeeded", signatureOverride: "0000000000000000000000000000000000000000000000000000000000000000");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Webhook_Should_NotDoubleApply_When_SameEventIdReplayed()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _, sku, order) = await PlaceOnlineOrderAsync(admin);
        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));

        var anonymous = _factory.CreateClient();
        var eventId = Guid.NewGuid();

        var first = await PostFakeWebhookRawAsync(anonymous, eventId, intent.IntentId, "succeeded");
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        await WaitForPaymentStatusAsync(customer, intent.IntentId, "Succeeded");
        var itemAfterFirst = await WaitForInventoryAsync(admin, sku, i => i.OnHand == 9 && i.Reserved == 0);

        var replayed = await PostFakeWebhookRawAsync(anonymous, eventId, intent.IntentId, "succeeded");
        replayed.StatusCode.Should().Be(HttpStatusCode.OK);

        // Give any erroneous re-processing a moment to happen, then assert nothing changed further.
        await Task.Delay(1000);
        var itemAfterReplay = await GetInventoryItemBySkuAsync(admin, sku);
        itemAfterReplay.OnHand.Should().Be(itemAfterFirst.OnHand);
        itemAfterReplay.Reserved.Should().Be(0);

        var paymentAfterReplay = await GetPaymentIntentAsync(customer, intent.IntentId);
        paymentAfterReplay.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task SimulateAfterCancel_Should_Fail_And_OrderStaysCancelled()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _, _, order) = await PlaceOnlineOrderAsync(admin);
        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));

        var cancelResponse = await CancelOrderRawAsync(customer, order.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForOrderStatusAsync(customer, order.Id, "Cancelled");

        // OrderCancelledIntegrationEvent cancels the order synchronously but reaches Payments' own
        // projection/payment asynchronously via the outbox -- wait for that side effect too, otherwise
        // simulate could race ahead of it and find the payment still Pending.
        await WaitForPaymentStatusAsync(customer, intent.IntentId, "Cancelled");

        var simulateResponse = await SimulateFakePaymentRawAsync(customer, intent.IntentId, "success");
        ((int)simulateResponse.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);

        (await GetOrderAsync(customer, order.Id)).Status.Should().Be("Cancelled");
    }
}

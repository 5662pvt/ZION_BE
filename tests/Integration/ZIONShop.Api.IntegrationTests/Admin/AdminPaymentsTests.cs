using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Payments.PaymentsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Admin;

[Collection(ApiTestCollection.Name)]
public class AdminPaymentsTests
{
    private readonly ApiFactory _factory;

    public AdminPaymentsTests(ApiFactory factory) => _factory = factory;

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

    private static async Task<List<AdminPaymentData>> ListAdminPaymentsAsync(
        HttpClient admin, string? status = null, string? channel = null, Guid? orderId = null)
    {
        var query = new List<string>();
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (channel is not null) query.Add($"channel={Uri.EscapeDataString(channel)}");
        if (orderId.HasValue) query.Add($"orderId={orderId.Value}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

        var response = await admin.GetAsync($"/api/v1/admin/payments{qs}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<AdminPaymentData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private async Task<(HttpClient Customer, OrderData Order)> PlaceOnlineOrderAsync(HttpClient admin)
    {
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "Online");
        await WaitForOrderStatusAsync(customer, placed.Id, "AwaitingPayment");

        return (customer, placed);
    }

    [Fact]
    public async Task List_Should_IncludePayment_When_IntentCreatedForOnlineOrder()
    {
        var admin = await AdminClientAsync();
        var (customer, order) = await PlaceOnlineOrderAsync(admin);

        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"), "EWallet");

        var payments = await ListAdminPaymentsAsync(admin, orderId: order.Id);

        var entry = payments.Should().ContainSingle().Subject;
        entry.OrderId.Should().Be(order.Id);
        entry.Status.Should().Be("Pending");
        entry.Channel.Should().Be("EWallet");
        entry.Amount.Should().Be(order.Total);
    }

    [Fact]
    public async Task List_Should_FilterByStatus_When_PaymentSucceeded()
    {
        var admin = await AdminClientAsync();
        var (customer, order) = await PlaceOnlineOrderAsync(admin);

        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));
        await SimulateFakePaymentAsync(customer, intent.IntentId, "success");

        var succeeded = await ListAdminPaymentsAsync(admin, status: "Succeeded", orderId: order.Id);
        succeeded.Should().ContainSingle(p => p.OrderId == order.Id);

        var pending = await ListAdminPaymentsAsync(admin, status: "Pending", orderId: order.Id);
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task List_Should_FilterByChannel_When_ChannelProvided()
    {
        var admin = await AdminClientAsync();
        var (customer, order) = await PlaceOnlineOrderAsync(admin);
        await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"), "BankTransfer");

        var matching = await ListAdminPaymentsAsync(admin, channel: "BankTransfer", orderId: order.Id);
        matching.Should().ContainSingle(p => p.OrderId == order.Id);

        var nonMatching = await ListAdminPaymentsAsync(admin, channel: "Card", orderId: order.Id);
        nonMatching.Should().BeEmpty();
    }

    [Fact]
    public async Task List_Should_ReturnForbidden_When_CallerIsCustomer()
    {
        var (client, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await client.GetAsync("/api/v1/admin/payments");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

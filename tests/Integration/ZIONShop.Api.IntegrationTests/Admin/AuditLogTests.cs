using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Admin;

[Collection(ApiTestCollection.Name)]
public class AuditLogTests
{
    private readonly ApiFactory _factory;

    public AuditLogTests(ApiFactory factory) => _factory = factory;

    private record AuditLogData(
        Guid Id, DateTime OccurredAt, Guid? ActorId, string? ActorEmail, string Action, string ResourceType,
        string? ResourceId, string HttpMethod, string Path, int StatusCode, string? IpAddress, string? CorrelationId, string? TraceId);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<List<AuditLogData>> GetAuditLogsAsync(HttpClient admin, string? action = null, string? actor = null, string? resourceType = null)
    {
        var query = new List<string>();
        if (action is not null) query.Add($"action={Uri.EscapeDataString(action)}");
        if (actor is not null) query.Add($"actor={Uri.EscapeDataString(actor)}");
        if (resourceType is not null) query.Add($"resourceType={Uri.EscapeDataString(resourceType)}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

        var response = await admin.GetAsync($"/api/v1/admin/audit-logs{qs}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<AuditLogData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private async Task<Guid> PlaceAndConfirmOrderAsync(HttpClient admin)
    {
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");
        return placed.Id;
    }

    [Fact]
    public async Task Ship_Should_WriteOneAuditLogRow_When_AdminShipsOrder()
    {
        var admin = await AdminClientAsync();
        var orderId = await PlaceAndConfirmOrderAsync(admin);

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{orderId}/ship", content: null);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logs = await GetAuditLogsAsync(admin, action: "Order.Ship");
        var entry = logs.Should().ContainSingle(l => l.ResourceId == orderId.ToString()).Subject;

        entry.ResourceType.Should().Be("Order");
        entry.StatusCode.Should().Be(200);
        entry.ActorEmail.Should().Be(TestAuthHelpers.AdminEmail);
        entry.HttpMethod.Should().Be("POST");
    }

    [Fact]
    public async Task Ship_Should_WriteNothing_When_SecondShipFails()
    {
        var admin = await AdminClientAsync();
        var orderId = await PlaceAndConfirmOrderAsync(admin);

        var first = await admin.PostAsync($"/api/v1/orders/{orderId}/ship", content: null);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await admin.PostAsync($"/api/v1/orders/{orderId}/ship", content: null);
        ((int)second.StatusCode).Should().BeGreaterThanOrEqualTo(400);

        var logs = await GetAuditLogsAsync(admin, action: "Order.Ship");
        logs.Where(l => l.ResourceId == orderId.ToString()).Should().ContainSingle();
    }

    [Fact]
    public async Task Cancel_Should_WriteNothing_When_CustomerCancelsOwnOrder()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        var cancelResponse = await CancelOrderRawAsync(customer, placed.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logs = await GetAuditLogsAsync(admin, action: "Order.Cancel");
        logs.Should().NotContain(l => l.ResourceId == placed.Id.ToString());
    }

    [Fact]
    public async Task List_Should_FilterByActor_When_ActorProvided()
    {
        var admin = await AdminClientAsync();
        var orderId = await PlaceAndConfirmOrderAsync(admin);

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{orderId}/ship", content: null);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var logs = await GetAuditLogsAsync(admin, actor: TestAuthHelpers.AdminEmail);
        logs.Should().Contain(l => l.ResourceId == orderId.ToString() && l.Action == "Order.Ship");
    }

    [Fact]
    public async Task List_Should_ReturnForbidden_When_CallerIsCustomer()
    {
        var (client, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await client.GetAsync("/api/v1/admin/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}

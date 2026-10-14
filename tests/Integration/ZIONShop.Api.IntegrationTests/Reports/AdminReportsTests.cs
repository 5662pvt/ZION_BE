using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Reports;

public record KpiValueData(decimal Value, decimal Previous, decimal? ChangePercent);
public record KpiCountData(int Value, int Previous, decimal? ChangePercent);
public record DashboardKpisData(
    KpiValueData Revenue, KpiCountData CompletedOrders, KpiValueData AverageOrderValue, KpiCountData NewCustomers,
    KpiCountData PlacedOrders, KpiCountData CancelledOrders, KpiValueData DiscountTotal, int AwaitingShipment, int LowStockProducts);
public record DashboardReportData(string From, string To, string Granularity, DashboardKpisData Kpis);
public record TopProductData(Guid ProductId, string ProductName, int Quantity, decimal Revenue, int Orders);

[Collection(ApiTestCollection.Name)]
public class AdminReportsTests
{
    private readonly ApiFactory _factory;

    public AdminReportsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(Guid OrderId, Guid ProductId, decimal Total)> CreateCompletedOrderAsync(HttpClient admin)
    {
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 150000m);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 2);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null);
        shipResponse.EnsureSuccessStatusCode();
        var completeResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        var total = placed.Subtotal - placed.DiscountAmount + placed.ShippingFee;
        return (placed.Id, productId, total);
    }

    [Fact]
    public async Task Dashboard_Should_Return200_And_IncludeRevenue_When_CompletedOrderExists()
    {
        var admin = await AdminClientAsync();
        var (_, _, total) = await CreateCompletedOrderAsync(admin);

        var response = await admin.GetAsync("/api/v1/admin/reports/dashboard");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<DashboardReportData>>(ApiEnvelopeJson.Options);
        var data = envelope!.Data!;

        data.Kpis.Revenue.Value.Should().BeGreaterThanOrEqualTo(total);
        data.Kpis.CompletedOrders.Value.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Dashboard_Should_Return403_When_CallerIsNotAdmin()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await customer.GetAsync("/api/v1/admin/reports/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Dashboard_Should_Return401_When_CallerIsAnonymous()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/admin/reports/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Dashboard_Should_Return400_When_FromIsAfterTo()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/reports/dashboard?from=2026-01-10&to=2026-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TopProducts_Should_ContainTheCompletedOrderProduct()
    {
        var admin = await AdminClientAsync();
        var (_, productId, _) = await CreateCompletedOrderAsync(admin);

        var response = await admin.GetAsync("/api/v1/admin/reports/top-products");
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<TopProductData>>>(ApiEnvelopeJson.Options);

        envelope!.Data!.Should().Contain(p => p.ProductId == productId);
    }

    [Fact]
    public async Task ExportRevenueCsv_Should_Return200_And_StartWithUtf8Bom()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/reports/export/revenue.csv");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().BeEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF });
    }
}

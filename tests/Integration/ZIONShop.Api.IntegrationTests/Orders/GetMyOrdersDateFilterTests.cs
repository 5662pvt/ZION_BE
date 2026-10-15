using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Orders;

[Collection(ApiTestCollection.Name)]
public class GetMyOrdersDateFilterTests
{
    private readonly ApiFactory _factory;

    public GetMyOrdersDateFilterTests(ApiFactory factory) => _factory = factory;

    private record OrderSummaryData(Guid Id, string Code, string Status, decimal Total, string Currency, DateTime CreatedAt, int ItemCount);

    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);
    private static DateOnly TodayVn() => DateOnly.FromDateTime(DateTime.UtcNow + VnOffset);

    private async Task<(HttpClient Customer, Guid OrderId)> PlaceOrderAsOwnCustomerAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var order = await PlaceOrderAsync(customer, addressId, "COD");
        return (customer, order.Id);
    }

    [Fact]
    public async Task GetMine_Should_IncludeOrder_When_FromToIsToday()
    {
        var (customer, orderId) = await PlaceOrderAsOwnCustomerAsync();
        var today = TodayVn().ToString("yyyy-MM-dd");

        var response = await customer.GetAsync($"/api/v1/orders/mine?page=1&pageSize=50&from={today}&to={today}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<OrderSummaryData>>>(ApiEnvelopeJson.Options);

        envelope!.Data.Should().Contain(o => o.Id == orderId);
    }

    [Fact]
    public async Task GetMine_Should_ExcludeOrder_When_RangeIsYesterday()
    {
        var (customer, orderId) = await PlaceOrderAsOwnCustomerAsync();
        var yesterday = TodayVn().AddDays(-1).ToString("yyyy-MM-dd");

        var response = await customer.GetAsync($"/api/v1/orders/mine?page=1&pageSize=50&from={yesterday}&to={yesterday}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<OrderSummaryData>>>(ApiEnvelopeJson.Options);

        envelope!.Data.Should().NotContain(o => o.Id == orderId);
    }

    [Fact]
    public async Task GetMine_Should_Return400_When_FromIsAfterTo()
    {
        var (customer, _) = await PlaceOrderAsOwnCustomerAsync();
        var today = TodayVn();

        var response = await customer.GetAsync($"/api/v1/orders/mine?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<OrderSummaryData>>>(ApiEnvelopeJson.Options);

        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.InvalidDateRange");
    }

    [Fact]
    public async Task GetMine_Should_Return400_When_FormatIsInvalid()
    {
        var (customer, _) = await PlaceOrderAsOwnCustomerAsync();

        var response = await customer.GetAsync("/api/v1/orders/mine?from=15-06-2026");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<OrderSummaryData>>>(ApiEnvelopeJson.Options);

        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.InvalidDateRange");
    }
}

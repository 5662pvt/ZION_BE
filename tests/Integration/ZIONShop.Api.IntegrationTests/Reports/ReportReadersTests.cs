using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Promotions;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Reports;

[Collection(ApiTestCollection.Name)]
public class ReportReadersTests
{
    private readonly ApiFactory _factory;

    public ReportReadersTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task GetCompletedOrdersAsync_Should_ReturnOrder_When_RangeContainsCompletion_And_Exclude_When_RangeIsInThePast()
    {
        var admin = await AdminClientAsync();
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

        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOrderReportReader>();

        var now = DateTime.UtcNow;
        var current = await reader.GetCompletedOrdersAsync(now.AddMinutes(-5), now.AddMinutes(5));
        var row = current.Should().ContainSingle(r => r.OrderId == placed.Id).Subject;
        row.Subtotal.Should().Be(300000m);
        row.ShippingFee.Should().Be(placed.ShippingFee);
        row.DiscountAmount.Should().Be(0m);
        row.PaymentMethod.Should().Be("COD");
        row.Lines.Should().ContainSingle(l => l.ProductId == productId && l.Quantity == 2 && l.UnitPrice == 150000m && !l.IsGift);
        row.CompletedAtUtc.Should().BeOnOrAfter(now.AddMinutes(-5)).And.BeOnOrBefore(now.AddMinutes(5));

        var past = await reader.GetCompletedOrdersAsync(now.AddDays(-10), now.AddDays(-5));
        past.Should().NotContain(r => r.OrderId == placed.Id);
    }

    [Fact]
    public async Task GetPlacedStatusCountsAsync_Should_ContainAllOrderStatuses_And_CountPlacedOrder()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        await PlaceOrderAsync(customer, addressId, "COD");

        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IOrderReportReader>();

        var now = DateTime.UtcNow;
        var counts = await reader.GetPlacedStatusCountsAsync(now.AddMinutes(-5), now.AddMinutes(5));

        counts.Keys.Should().Contain(new[] { "PendingStock", "AwaitingPayment", "Confirmed", "Paid", "Shipped", "Completed", "Cancelled" });
        counts.Values.Sum().Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task CountCustomersRegisteredAsync_Should_IncludeNewlyRegisteredCustomer()
    {
        var client = _factory.CreateClient();
        var email = $"report-customer-{Guid.NewGuid():N}@zionshop.local";
        var before = DateTime.UtcNow.AddMinutes(-1);

        await client.RegisterAndConfirmAsync(_factory, email, "Customer@123", "Report Customer");

        var after = DateTime.UtcNow.AddMinutes(1);

        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IUserStatsReader>();

        var count = await reader.CountCustomersRegisteredAsync(before, after);
        count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ResolveCouponCodesAsync_Should_ResolvePromotion_CaseInsensitive()
    {
        var admin = await AdminClientAsync();
        var promotion = await CreatePromotionAsync(admin, type: "PercentageOff", value: 10m);
        await ActivatePromotionAsync(admin, promotion.Id);
        var code = $"ZRPT{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var coupon = await CreateCouponAsync(admin, promotion.Id, code);

        using var scope = _factory.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IPromotionLookup>();

        var lowerCode = coupon.Code.ToLowerInvariant();
        var result = await lookup.ResolveCouponCodesAsync(new[] { lowerCode });

        result.Should().ContainKey(lowerCode);
        result[lowerCode].PromotionId.Should().Be(promotion.Id);
        result[lowerCode].Type.Should().Be("PercentageOff");
    }

    [Fact]
    public async Task CategoryLookup_Should_ReturnSeededCategory()
    {
        var admin = await AdminClientAsync();
        var categoryId = await CreateCategoryAsync(admin);

        using var scope = _factory.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ICategoryLookup>();

        var categories = await lookup.ListAsync();

        categories.Should().Contain(c => c.CategoryId == categoryId);
    }

    [Fact]
    public async Task CountLowStockAsync_Should_Increase_When_StockDroppedToThreshold_And_Restore()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 20);

        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IInventoryStatsReader>();

        var baseline = await reader.CountLowStockAsync();

        await AdjustStockAsync(admin, productId, -16, "lower below threshold");
        var lowered = await reader.CountLowStockAsync();
        lowered.Should().BeGreaterThan(baseline);

        await AdjustStockAsync(admin, productId, 16, "restore");
        var restored = await reader.CountLowStockAsync();
        restored.Should().Be(baseline);
    }
}

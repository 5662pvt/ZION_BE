using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Promotions;

[Collection(ApiTestCollection.Name)]
public class PromotionScopesTests
{
    private readonly ApiFactory _factory;

    public PromotionScopesTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task PlaceOrder_Should_DiscountOnlyTheMatchingCategoryLine_When_CouponScopeIsCategories()
    {
        var admin = await AdminClientAsync();
        var targetCategoryId = await CreateCategoryAsync(admin);
        var otherCategoryId = await CreateCategoryAsync(admin);

        var matchingProductId = await CreatePublishedProductInCategoryAsync(admin, targetCategoryId, price: 100000m);
        var otherProductId = await CreatePublishedProductInCategoryAsync(admin, otherCategoryId, price: 200000m);
        await AdjustStockAsync(admin, matchingProductId, 5);
        await AdjustStockAsync(admin, otherProductId, 5);

        var (_, code) = await SeedScopedPromotionAsync(
            admin, "CATSCOPE20", type: "PercentageOff", value: 20m, scope: "Categories",
            targetCategoryIds: new[] { targetCategoryId });

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, matchingProductId, 1);
        await AddToCartAsync(customer, otherProductId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD", couponCode: code);

        placed.Subtotal.Should().Be(300000m);
        placed.DiscountAmount.Should().Be(20000m);
        placed.ShippingFee.Should().Be(30000m);
        placed.Total.Should().Be(310000m);
    }

    [Fact]
    public async Task PlaceOrder_Should_DiscountOnlyTheMatchingProductLine_When_CouponScopeIsProducts()
    {
        var admin = await AdminClientAsync();
        var matchingProductId = await CreatePublishedProductInCategoryAsync(admin, null, price: 150000m);
        var otherProductId = await CreatePublishedProductInCategoryAsync(admin, null, price: 150000m);
        await AdjustStockAsync(admin, matchingProductId, 5);
        await AdjustStockAsync(admin, otherProductId, 5);

        var (_, code) = await SeedScopedPromotionAsync(
            admin, "PRODSCOPE50K", type: "FixedAmountOff", value: 50000m, scope: "Products",
            targetProductIds: new[] { matchingProductId });

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, matchingProductId, 1);
        await AddToCartAsync(customer, otherProductId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD", couponCode: code);

        placed.Subtotal.Should().Be(300000m);
        placed.DiscountAmount.Should().Be(50000m);
        placed.Total.Should().Be(280000m);
    }

    [Fact]
    public async Task PlaceOrder_Should_Return400_When_CouponScopeIsProductsAndCartHasNoMatchingProduct()
    {
        var admin = await AdminClientAsync();
        var targetProductId = Guid.NewGuid();
        var cartProductId = await CreatePublishedProductInCategoryAsync(admin, null, price: 100000m);
        await AdjustStockAsync(admin, cartProductId, 5);

        var (_, code) = await SeedScopedPromotionAsync(
            admin, "PRODNOMATCH", type: "FixedAmountOff", value: 50000m, scope: "Products",
            targetProductIds: new[] { targetProductId });

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, cartProductId, 1);

        var response = await PlaceOrderRawAsync(customer, addressId, "COD", couponCode: code);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Mã giảm giá không áp dụng cho sản phẩm trong giỏ");
    }

    [Fact]
    public async Task PlaceOrder_Should_WaiveShippingFee_When_CouponTypeIsFreeShippingAndOrderIsBelowFreeShippingThreshold()
    {
        var admin = await AdminClientAsync();
        var productId = await CreatePublishedProductInCategoryAsync(admin, null, price: 199000m);
        await AdjustStockAsync(admin, productId, 5);

        var (_, code) = await SeedScopedPromotionAsync(admin, "FREESHIP1", type: "FreeShipping", value: 0m, scope: "Order");

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD", couponCode: code);

        placed.Subtotal.Should().Be(199000m);
        placed.ShippingFee.Should().Be(30000m);
        placed.DiscountAmount.Should().Be(30000m);
        placed.Total.Should().Be(placed.Subtotal);
    }

    [Fact]
    public async Task PlaceOrder_Should_Return400_When_CouponTypeIsFreeShippingAndOrderAlreadyShipsFree()
    {
        var admin = await AdminClientAsync();
        var productId = await CreatePublishedProductInCategoryAsync(admin, null, price: 700000m);
        await AdjustStockAsync(admin, productId, 5);

        var (_, code) = await SeedScopedPromotionAsync(admin, "FREESHIP2", type: "FreeShipping", value: 0m, scope: "Order");

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var response = await PlaceOrderRawAsync(customer, addressId, "COD", couponCode: code);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Đơn hàng đã được miễn phí vận chuyển");
    }
}

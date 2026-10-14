using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Payments.PaymentsTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Checkout;

[Collection(ApiTestCollection.Name)]
public class CheckoutWithCouponTests
{
    private readonly ApiFactory _factory;

    public CheckoutWithCouponTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<int> GetMyOrdersCountAsync(HttpClient customer)
    {
        var response = await customer.GetAsync("/api/v1/orders/mine?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<object>>>(ApiEnvelopeJson.Options);
        return envelope!.Pagination!.TotalCount;
    }

    [Fact]
    public async Task Coupon_Should_TrackUsageAcrossOrderLifecycle_When_SameCustomerReusesCode()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 700000m);
        await AdjustStockAsync(admin, productId, 10);

        var promotion = await CreatePromotionAsync(
            admin, type: "PercentageOff", value: 10m, maxDiscountAmount: 50000m,
            minOrderSubtotal: 300000m, usageLimit: 5, perUserLimit: 1);
        var coupon = await CreateCouponAsync(admin, promotion.Id, "LOYAL10PCT");
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);

        // Step 1: first order applies the coupon and settles to Confirmed.
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD", couponCode: "loyal10pct");

        placed.CouponCode.Should().Be("LOYAL10PCT");
        placed.Subtotal.Should().Be(700000m);
        placed.DiscountAmount.Should().Be(50000m);
        placed.ShippingFee.Should().Be(0m);
        placed.Total.Should().Be(650000m);

        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");
        var afterFirstOrder = await WaitForPromotionUsedCountAsync(admin, promotion.Id, 1);
        afterFirstOrder.UsedCount.Should().Be(1);

        // Step 2: a second order with the same code is rejected by the per-user limit; nothing is created.
        await AddToCartAsync(customer, productId, 1);
        var ordersBefore = await GetMyOrdersCountAsync(customer);

        var secondAttempt = await PlaceOrderRawAsync(customer, addressId, "COD", couponCode: coupon.Code);
        secondAttempt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var secondEnvelope = await secondAttempt.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        secondEnvelope!.Message.Should().Be("Bạn đã sử dụng hết lượt cho mã này");

        var ordersAfter = await GetMyOrdersCountAsync(customer);
        ordersAfter.Should().Be(ordersBefore);

        // Step 3: cancelling the first order releases the usage, letting the customer use the code again.
        var cancelResponse = await CancelOrderRawAsync(customer, placed.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");
        await WaitForPromotionUsedCountAsync(admin, promotion.Id, 0);

        var thirdOrder = await PlaceOrderAsync(customer, addressId, "COD", couponCode: coupon.Code);
        thirdOrder.CouponCode.Should().Be("LOYAL10PCT");
        thirdOrder.DiscountAmount.Should().Be(50000m);
    }

    [Fact]
    public async Task PlaceOrder_Should_Return400_And_LeaveCartUntouched_When_SubtotalBelowCouponMinimum()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AdjustStockAsync(admin, productId, 5);

        var promotion = await CreatePromotionAsync(admin, type: "PercentageOff", value: 10m, minOrderSubtotal: 500000m);
        var coupon = await CreateCouponAsync(admin, promotion.Id, "MINGATE1");
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var response = await PlaceOrderRawAsync(customer, addressId, "COD", couponCode: coupon.Code);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Đơn hàng chưa đạt giá trị tối thiểu 500,000");

        var cart = await GetCartAsync(customer);
        cart.Items.Should().ContainSingle(i => i.ProductId == productId && i.Quantity == 1);
    }

    [Fact]
    public async Task Cod_Should_ApplyFixedAmountDiscount_Before_ShippingFee_When_SubtotalIsBelowFreeShippingThreshold()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 199000m);
        await AdjustStockAsync(admin, productId, 5);

        var promotion = await CreatePromotionAsync(admin, type: "FixedAmountOff", value: 100000m);
        var coupon = await CreateCouponAsync(admin, promotion.Id, "FIXED100K");
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD", couponCode: coupon.Code);

        placed.Subtotal.Should().Be(199000m);
        placed.DiscountAmount.Should().Be(100000m);
        placed.ShippingFee.Should().Be(30000m);
        placed.Total.Should().Be(129000m);
    }

    [Fact]
    public async Task Online_Should_CreatePaymentIntent_With_DiscountedTotal_When_CouponApplied()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 300000m);
        await AdjustStockAsync(admin, productId, 5);

        var promotion = await CreatePromotionAsync(admin, type: "FixedAmountOff", value: 50000m);
        var coupon = await CreateCouponAsync(admin, promotion.Id, "ONLINE50K");
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "Online", couponCode: coupon.Code);
        placed.ShippingFee.Should().Be(30000m);
        placed.Total.Should().Be(280000m);

        await WaitForOrderStatusAsync(customer, placed.Id, "AwaitingPayment");

        var intent = await CreatePaymentIntentAsync(customer, placed.Id, Guid.NewGuid().ToString("N"));
        intent.Amount.Should().Be(280000m);
    }
}

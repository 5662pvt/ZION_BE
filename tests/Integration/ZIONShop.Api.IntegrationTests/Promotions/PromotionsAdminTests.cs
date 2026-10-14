using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Promotions;

[Collection(ApiTestCollection.Name)]
public class PromotionsAdminTests
{
    private readonly ApiFactory _factory;

    public PromotionsAdminTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task ValidateCoupon_Should_ReturnDiscount_When_AdminCreatedActivatedPromotionWithCoupon()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 200000m);
        await AddToCartAsync(customer, productId, 1);

        var promotion = await CreatePromotionAsync(admin, type: "PercentageOff", value: 10m, minOrderSubtotal: 50000m);
        await CreateCouponAsync(admin, promotion.Id, "WELCOME10");
        await ActivatePromotionAsync(admin, promotion.Id);

        var result = await ValidateCouponAsync(customer, "welcome10");

        result.Code.Should().Be("WELCOME10");
        result.DiscountAmount.Should().Be(20000m);
    }

    [Fact]
    public async Task ValidateCoupon_Should_Return200_When_CalledByNonAdminCustomer()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AddToCartAsync(customer, productId, 1);

        var promotion = await CreatePromotionAsync(admin, type: "PercentageOff", value: 5m, minOrderSubtotal: 10000m);
        await CreateCouponAsync(admin, promotion.Id, "NONADMIN5");
        await ActivatePromotionAsync(admin, promotion.Id);

        var response = await ValidateCouponRawAsync(customer, "NONADMIN5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ValidateCoupon_Should_Return400_When_PromotionNotActivated()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AddToCartAsync(customer, productId, 1);

        var promotion = await CreatePromotionAsync(admin);
        await CreateCouponAsync(admin, promotion.Id, "INACTIVE1");

        var response = await ValidateCouponRawAsync(customer, "INACTIVE1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Mã giảm giá không còn hiệu lực");
    }

    [Fact]
    public async Task ValidateCoupon_Should_Return400_When_Expired()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AddToCartAsync(customer, productId, 1);

        var promotion = await CreatePromotionAsync(admin, startsAt: DateTime.UtcNow.AddDays(-10), endsAt: DateTime.UtcNow.AddDays(-1));
        await CreateCouponAsync(admin, promotion.Id, "EXPIRED1");
        await ActivatePromotionAsync(admin, promotion.Id);

        var response = await ValidateCouponRawAsync(customer, "EXPIRED1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Mã giảm giá đã hết hạn");
    }

    [Fact]
    public async Task ValidateCoupon_Should_Return400_When_SubtotalBelowMinimum()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AddToCartAsync(customer, productId, 1);

        var promotion = await CreatePromotionAsync(admin, minOrderSubtotal: 500000m);
        await CreateCouponAsync(admin, promotion.Id, "MINSUB1");
        await ActivatePromotionAsync(admin, promotion.Id);

        var response = await ValidateCouponRawAsync(customer, "MINSUB1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Đơn hàng chưa đạt giá trị tối thiểu 500,000");
    }

    [Fact]
    public async Task GenerateCoupons_Should_ReturnUniqueCodes_When_Requested()
    {
        var admin = await AdminClientAsync();
        var promotion = await CreatePromotionAsync(admin);

        var coupons = await GenerateCouponsAsync(admin, promotion.Id, "GEN", 5);

        coupons.Should().HaveCount(5);
        coupons.Select(c => c.Code).Distinct().Should().HaveCount(5);
        coupons.Should().OnlyContain(c => c.Code.StartsWith("GEN-"));
    }

    [Fact]
    public async Task CreateCoupon_Should_Return409_When_CodeAlreadyTaken()
    {
        var admin = await AdminClientAsync();
        var promotion = await CreatePromotionAsync(admin);
        await CreateCouponAsync(admin, promotion.Id, "DUPCODE1");

        var response = await CreateCouponRawAsync(admin, promotion.Id, "dupcode1");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}

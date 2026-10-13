using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Promotions;

[Collection(ApiTestCollection.Name)]
public class GiftPromotionsTests
{
    private readonly ApiFactory _factory;

    public GiftPromotionsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task Create_Should_Return400_When_GiftTypeHasNoGiftItems()
    {
        var admin = await AdminClientAsync();

        var response = await CreatePromotionRawAsync(admin, type: "Gift", scope: "Order", minOrderSubtotal: 100000m, giftItems: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("'giftItems' phải có 1-5 sản phẩm khi type là 'Gift'");
    }

    [Fact]
    public async Task Create_Should_Return400_When_GiftTypeScopeProductsMissingMinQuantity()
    {
        var admin = await AdminClientAsync();
        var (triggerProductId, _) = await CreatePublishedProductAsync(admin);
        var (giftProductId, _) = await CreatePublishedProductAsync(admin);

        var response = await CreatePromotionRawAsync(
            admin, type: "Gift", scope: "Products", targetProductIds: new[] { triggerProductId },
            minOrderSubtotal: 0m, giftItems: new[] { new GiftItemData(giftProductId, 1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("'minQuantity' là bắt buộc khi scope khác 'Order' với khuyến mãi quà tặng");
    }

    [Fact]
    public async Task CreateCoupon_Should_Return400_When_PromotionTypeIsGift()
    {
        var admin = await AdminClientAsync();
        var (giftProductId, _) = await CreatePublishedProductAsync(admin);

        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Order", minOrderSubtotal: 100000m,
            giftItems: new[] { new GiftItemData(giftProductId, 1) });

        var response = await CreateCouponRawAsync(admin, promotion.Id, "GIFTCODE1");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Khuyến mãi quà tặng không dùng mã giảm giá");
    }

    [Fact]
    public async Task GiftPreview_Should_ReturnGift_When_OrderScopeSubtotalMet()
    {
        var admin = await AdminClientAsync();
        var (cartProductId, _) = await CreatePublishedProductAsync(admin, price: 300000m);
        var (giftProductId, _) = await CreatePublishedProductAsync(admin, price: 50000m);
        await AdjustStockAsync(admin, cartProductId, 5);
        await AdjustStockAsync(admin, giftProductId, 5);

        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Order", minOrderSubtotal: 200000m,
            giftItems: new[] { new GiftItemData(giftProductId, 1) });
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, cartProductId, 1);

        var preview = await GetGiftPreviewAsync(customer);

        preview.Should().ContainSingle(p => p.PromotionId == promotion.Id && p.ProductId == giftProductId && p.Quantity == 1 && p.Available);
    }

    [Fact]
    public async Task GiftPreview_Should_ReturnEmpty_When_OrderScopeSubtotalNotMet()
    {
        var admin = await AdminClientAsync();
        var (cartProductId, _) = await CreatePublishedProductAsync(admin, price: 50000m);
        var (giftProductId, _) = await CreatePublishedProductAsync(admin, price: 50000m);
        await AdjustStockAsync(admin, cartProductId, 5);
        await AdjustStockAsync(admin, giftProductId, 5);

        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Order", minOrderSubtotal: 200000m,
            giftItems: new[] { new GiftItemData(giftProductId, 1) });
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, cartProductId, 1);

        var preview = await GetGiftPreviewAsync(customer);

        preview.Should().BeEmpty();
    }

    [Fact]
    public async Task GiftPreview_Should_ReturnGift_When_ProductsScopeMinQuantityMet()
    {
        var admin = await AdminClientAsync();
        var (triggerProductId, _) = await CreatePublishedProductAsync(admin, price: 100000m);
        var (giftProductId, _) = await CreatePublishedProductAsync(admin, price: 50000m);
        await AdjustStockAsync(admin, triggerProductId, 5);
        await AdjustStockAsync(admin, giftProductId, 5);

        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Products", targetProductIds: new[] { triggerProductId },
            minQuantity: 2, minOrderSubtotal: 0m,
            giftItems: new[] { new GiftItemData(giftProductId, 1) });
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, triggerProductId, 2);

        var preview = await GetGiftPreviewAsync(customer);

        preview.Should().ContainSingle(p => p.PromotionId == promotion.Id && p.ProductId == giftProductId && p.Available);
    }
}

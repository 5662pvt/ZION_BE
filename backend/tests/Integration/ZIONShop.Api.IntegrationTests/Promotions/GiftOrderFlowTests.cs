using System.Net;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Promotions.PromotionsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Promotions;

[Collection(ApiTestCollection.Name)]
public class GiftOrderFlowTests
{
    private readonly ApiFactory _factory;

    public GiftOrderFlowTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task GiftOrder_Should_GrantProductsScopeGift_TrackUsage_RequireMinQuantity_AndReleaseOnCancel()
    {
        var admin = await AdminClientAsync();
        var (triggerProductId, _) = await CreatePublishedProductAsync(admin, price: 10000m);
        var (giftProductId, _) = await CreatePublishedProductAsync(admin, price: 15000m);
        await AdjustStockAsync(admin, triggerProductId, 20);
        await AdjustStockAsync(admin, giftProductId, 10);

        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Products", targetProductIds: new[] { triggerProductId },
            minQuantity: 2, minOrderSubtotal: 0m,
            giftItems: new[] { new GiftItemData(giftProductId, 1) });
        await ActivatePromotionAsync(admin, promotion.Id);

        var giftAvailableBefore = await GetAvailabilityAsync(admin, giftProductId);

        // Step 1: cart meets minQuantity(2) on the trigger product -> gift line is auto-applied,
        // free (unitPrice/lineTotal 0), and does not inflate the subtotal.
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, triggerProductId, 2);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        placed.Subtotal.Should().Be(20000m);
        placed.Lines.Should().ContainSingle(l => l.ProductId == giftProductId && l.IsGift && l.UnitPrice == 0m && l.LineTotal == 0m);

        // Step 2: once the reservation settles, the gift stock is committed and promotion usage recorded.
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");
        await WaitForPromotionUsedCountAsync(admin, promotion.Id, 1);
        await WaitForAvailabilityAsync(admin, giftProductId, a => a.Available == giftAvailableBefore.Available - 1);

        // Step 3: a cart that doesn't reach minQuantity gets no gift line at all.
        var (otherCustomer, otherAddressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(otherCustomer, triggerProductId, 1);
        var placedWithoutGift = await PlaceOrderAsync(otherCustomer, otherAddressId, "COD");
        placedWithoutGift.Lines.Should().NotContain(l => l.IsGift);

        // Step 4: cancelling the first order releases both the promotion usage and the reserved gift stock.
        var cancelResponse = await CancelOrderRawAsync(customer, placed.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");
        await WaitForPromotionUsedCountAsync(admin, promotion.Id, 0);
        await WaitForAvailabilityAsync(admin, giftProductId, a => a.Available == giftAvailableBefore.Available);
    }

    [Fact]
    public async Task PlaceOrder_Should_DropOutOfStockGiftLine_But_KeepAvailableGiftLine_When_OrderScopeGiftHasInsufficientStock()
    {
        var admin = await AdminClientAsync();
        var (paidProductId, _) = await CreatePublishedProductAsync(admin, price: 60000m);
        var (outOfStockGiftId, _) = await CreatePublishedProductAsync(admin, price: 5000m);
        var (inStockGiftId, _) = await CreatePublishedProductAsync(admin, price: 5000m);
        await AdjustStockAsync(admin, paidProductId, 10);
        // outOfStockGiftId is never stocked, so it stays at 0 available (adjust rejects a zero delta).
        await AdjustStockAsync(admin, inStockGiftId, 10);

        // minOrderSubtotal is kept above 50,000 so this promotion can't leak into
        // GiftPromotionsTests.GiftPreview_Should_ReturnEmpty_When_OrderScopeSubtotalNotMet, whose cart
        // subtotal is fixed at 50,000 and expects zero qualifying gift promotions of any kind.
        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Order", minOrderSubtotal: 60000m,
            giftItems: new[] { new GiftItemData(outOfStockGiftId, 1), new GiftItemData(inStockGiftId, 1) });
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, paidProductId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        placed.Lines.Should().ContainSingle(l => l.ProductId == inStockGiftId && l.IsGift);
        placed.Lines.Should().NotContain(l => l.ProductId == outOfStockGiftId);

        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");
        await WaitForPromotionUsedCountAsync(admin, promotion.Id, 1);
    }

    [Fact]
    public async Task PlaceOrder_Should_CancelImmediately_AndReleaseGiftUsage_When_PaidProductReservationFails()
    {
        var admin = await AdminClientAsync();
        var (paidProductId, _) = await CreatePublishedProductAsync(admin, price: 70000m);
        var (giftProductId, _) = await CreatePublishedProductAsync(admin, price: 5000m);
        // paidProductId is never stocked, so it stays at 0 available and the reservation fails outright.
        await AdjustStockAsync(admin, giftProductId, 10);

        var promotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Order", minOrderSubtotal: 70000m,
            giftItems: new[] { new GiftItemData(giftProductId, 1) });
        await ActivatePromotionAsync(admin, promotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, paidProductId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        // Reservation failure releases coupon/gift usage inline (InventoryReservationFailedIntegrationEventHandler),
        // not via the 30-minute StaleReservationReleaseProcessor, so a short timeout is enough here.
        var cancelled = await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled", TimeSpan.FromSeconds(10));
        cancelled.CancelReason.Should().Contain("Hết hàng");

        await WaitForPromotionUsedCountAsync(admin, promotion.Id, 0, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task PlaceOrder_Should_IncludeBothGiftLines_When_TwoGiftPromotionsQualifySimultaneously()
    {
        var admin = await AdminClientAsync();
        var (paidProductId, _) = await CreatePublishedProductAsync(admin, price: 80000m);
        var (firstGiftId, _) = await CreatePublishedProductAsync(admin, price: 5000m);
        var (secondGiftId, _) = await CreatePublishedProductAsync(admin, price: 5000m);
        await AdjustStockAsync(admin, paidProductId, 10);
        await AdjustStockAsync(admin, firstGiftId, 10);
        await AdjustStockAsync(admin, secondGiftId, 10);

        // minOrderSubtotal is kept above 50,000 so this promotion can't leak into
        // GiftPromotionsTests.GiftPreview_Should_ReturnEmpty_When_OrderScopeSubtotalNotMet, whose cart
        // subtotal is fixed at 50,000 and expects zero qualifying gift promotions of any kind.
        var orderScopePromotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Order", minOrderSubtotal: 80000m,
            giftItems: new[] { new GiftItemData(firstGiftId, 1) });
        await ActivatePromotionAsync(admin, orderScopePromotion.Id);

        var productsScopePromotion = await CreatePromotionAsync(
            admin, type: "Gift", scope: "Products", targetProductIds: new[] { paidProductId },
            minQuantity: 1, minOrderSubtotal: 0m,
            giftItems: new[] { new GiftItemData(secondGiftId, 1) });
        await ActivatePromotionAsync(admin, productsScopePromotion.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, paidProductId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        placed.Lines.Should().ContainSingle(l => l.ProductId == firstGiftId && l.IsGift);
        placed.Lines.Should().ContainSingle(l => l.ProductId == secondGiftId && l.IsGift);

        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");
        await WaitForPromotionUsedCountAsync(admin, orderScopePromotion.Id, 1);
        await WaitForPromotionUsedCountAsync(admin, productsScopePromotion.Id, 1);
    }
}

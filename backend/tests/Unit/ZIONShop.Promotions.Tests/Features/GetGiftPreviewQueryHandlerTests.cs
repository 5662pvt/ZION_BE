using ZIONShop.Contracts.Cart;
using ZIONShop.Promotions.Application.Features.GetGiftPreview;
using ZIONShop.Promotions.Tests.Fakes;

namespace ZIONShop.Promotions.Tests.Features;

public class GetGiftPreviewQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnEmpty_When_CartIsEmpty()
    {
        var userId = Guid.NewGuid();
        var handler = new GetGiftPreviewQueryHandler(
            new FakeCartReader(), new FakeGiftPromotionEvaluator(), new FakeStockAvailabilityReader(), new FakeProductPriceLookup());

        var result = await handler.Handle(new GetGiftPreviewQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnAvailableTrue_When_StockCoversGiftAndExistingCartQuantity()
    {
        var userId = Guid.NewGuid();
        var cartProductId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(cartProductId, "Widget", 1, 300000m));
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, "Buy 1 Get Gift", giftProductId, 1);
        var stock = new FakeStockAvailabilityReader().WithAvailable(giftProductId, 5);
        var prices = new FakeProductPriceLookup().Add(giftProductId, "Cap", 50000m, primaryImageUrl: "https://example.com/cap.png");

        var handler = new GetGiftPreviewQueryHandler(cart, giftEvaluator, stock, prices);
        var result = await handler.Handle(new GetGiftPreviewQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        var preview = result.Value.Single();
        preview.PromotionId.Should().Be(promotionId);
        preview.ProductName.Should().Be("Cap");
        preview.ImageUrl.Should().Be("https://example.com/cap.png");
        preview.Available.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnAvailableFalse_When_StockBelowGiftQuantityPlusCartQuantity()
    {
        var userId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        // The gift product is itself already in the cart (quantity 2); only 2 more available in stock,
        // which covers the existing cart line but not the extra gift quantity (1) on top of it.
        var cart = new FakeCartReader().WithCart(userId, "VND", new CartSnapshotLine(giftProductId, "Cap", 2, 50000m));
        var giftEvaluator = new FakeGiftPromotionEvaluator().WithGrant(promotionId, "Buy 1 Get Gift", giftProductId, 1);
        var stock = new FakeStockAvailabilityReader().WithAvailable(giftProductId, 2);
        var prices = new FakeProductPriceLookup().Add(giftProductId, "Cap", 50000m);

        var handler = new GetGiftPreviewQueryHandler(cart, giftEvaluator, stock, prices);
        var result = await handler.Handle(new GetGiftPreviewQuery(userId), CancellationToken.None);

        result.Value.Single().Available.Should().BeFalse();
    }
}

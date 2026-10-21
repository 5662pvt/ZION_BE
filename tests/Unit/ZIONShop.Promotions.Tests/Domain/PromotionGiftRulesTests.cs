using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;

namespace ZIONShop.Promotions.Tests.Domain;

public class PromotionGiftRulesTests
{
    private static Promotion ActivePromotion() =>
        Promotion.Create(
            "Base", null, PromotionType.PercentageOff, 10m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null).Value;

    [Fact]
    public void Create_Should_Fail_When_GiftTypeHasNoGiftItems()
    {
        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, giftItems: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsRequired");
    }

    [Fact]
    public void Create_Should_Fail_When_GiftTypeHasMoreThanFiveItems()
    {
        var items = Enumerable.Range(0, 6).Select(_ => new GiftItemInput(Guid.NewGuid(), 1)).ToList();

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsTooMany");
    }

    [Fact]
    public void Create_Should_Fail_When_GiftItemsHaveDuplicateProduct()
    {
        var productId = Guid.NewGuid();
        var items = new[] { new GiftItemInput(productId, 1), new GiftItemInput(productId, 2) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsDuplicateProduct");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_Should_Fail_When_GiftQuantityOutOfRange(int quantity)
    {
        var items = new[] { new GiftItemInput(Guid.NewGuid(), quantity) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftQuantityInvalid");
    }

    [Fact]
    public void Create_Should_Fail_When_OrderScopeHasMinQuantity()
    {
        var items = new[] { new GiftItemInput(Guid.NewGuid(), 1) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, minQuantity: 2, giftItems: items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.MinQuantityNotAllowedForOrderScope");
    }

    [Fact]
    public void Create_Should_Fail_When_OrderScopeMinSubtotalIsZero()
    {
        var items = new[] { new GiftItemInput(Guid.NewGuid(), 1) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.OrderScopeRequiresPositiveSubtotal");
    }

    [Fact]
    public void Create_Should_Fail_When_ProductsScopeMissingMinQuantity()
    {
        var items = new[] { new GiftItemInput(Guid.NewGuid(), 1) };
        var targetProductId = Guid.NewGuid();

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Products, new[] { targetProductId }, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.MinQuantityRequired");
    }

    [Fact]
    public void Create_Should_Fail_When_NonGiftTypeHasGiftItems()
    {
        var items = new[] { new GiftItemInput(Guid.NewGuid(), 1) };

        var result = Promotion.Create(
            "Percent", null, PromotionType.PercentageOff, 10m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsNotAllowed");
    }

    [Fact]
    public void Create_Should_Fail_When_NonGiftTypeHasMinQuantity()
    {
        var result = Promotion.Create(
            "Percent", null, PromotionType.PercentageOff, 10m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, minQuantity: 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.MinQuantityNotAllowed");
    }

    [Fact]
    public void Create_Should_Succeed_When_OrderScopeGiftIsValid()
    {
        var giftProductId = Guid.NewGuid();
        var items = new[] { new GiftItemInput(giftProductId, 2) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 200000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsSuccess.Should().BeTrue();
        result.Value.MinQuantity.Should().BeNull();
        result.Value.GiftItems.Should().ContainSingle(g => g.ProductId == giftProductId && g.Quantity == 2);
    }

    [Fact]
    public void Create_Should_Succeed_When_ProductsScopeGiftIsValid()
    {
        var giftProductId = Guid.NewGuid();
        var targetProductId = Guid.NewGuid();
        var items = new[] { new GiftItemInput(giftProductId, 1) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Products, new[] { targetProductId }, null, minQuantity: 2, giftItems: items);

        result.IsSuccess.Should().BeTrue();
        result.Value.MinQuantity.Should().Be(2);
    }

    [Fact]
    public void Update_Should_ReplaceGiftItems_When_PromotionIsGift()
    {
        var promotion = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, new[] { new GiftItemInput(Guid.NewGuid(), 1) }).Value;

        var newGiftProductId = Guid.NewGuid();
        var result = promotion.Update(
            "Gift v2", null, PromotionType.Gift, 0m, null, 150000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, new[] { new GiftItemInput(newGiftProductId, 3) });

        result.IsSuccess.Should().BeTrue();
        promotion.GiftItems.Should().ContainSingle(g => g.ProductId == newGiftProductId && g.Quantity == 3);
    }

    [Fact]
    public void Create_Should_Succeed_When_SameProductHasTwoDistinctGiftVariants()
    {
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();
        var items = new[] { new GiftItemInput(productId, 1, variantA), new GiftItemInput(productId, 1, variantB) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsSuccess.Should().BeTrue();
        result.Value.GiftItems.Should().HaveCount(2);
        result.Value.GiftItems.Should().ContainSingle(g => g.VariantId == variantA);
        result.Value.GiftItems.Should().ContainSingle(g => g.VariantId == variantB);
    }

    [Fact]
    public void Create_Should_Fail_When_SameVariantRepeated_Even_If_WrappedUnderDifferentInput()
    {
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var items = new[] { new GiftItemInput(productId, 1, variantId), new GiftItemInput(productId, 2, variantId) };

        var result = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.GiftItemsDuplicateProduct");
    }

    [Fact]
    public void Update_Should_Fail_When_UsageLimitBelowUsedCount()
    {
        var promotion = ActivePromotion();
        promotion.IncrementUsage();
        promotion.IncrementUsage();

        var result = promotion.Update(
            "Base", null, PromotionType.PercentageOff, 10m, null, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), usageLimit: 1, perUserLimit: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.UsageLimitBelowUsedCount");
    }
}

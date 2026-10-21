using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Infrastructure.Services;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Services;

public class GiftPromotionEvaluatorTests
{
    private static (PromotionsDbContext Db, GiftPromotionEvaluator Evaluator, FakeProductCatalogReader Catalog) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var promotions = new PromotionRepository(db);
        var redemptions = new GiftRedemptionRepository(db);
        var catalog = new FakeProductCatalogReader();
        var evaluator = new GiftPromotionEvaluator(promotions, redemptions, db, TimeProvider.System, catalog);
        return (db, evaluator, catalog);
    }

    private static async Task<Promotion> SeedActiveGiftPromotionAsync(
        PromotionsDbContext db,
        decimal minOrderSubtotal,
        PromotionScope scope,
        Guid[]? targetProductIds,
        Guid[]? targetCategoryIds,
        int? minQuantity,
        (Guid ProductId, int Quantity)[] giftItems,
        int? usageLimit = null,
        int? perUserLimit = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null)
    {
        var items = giftItems.Select(g => new GiftItemInput(g.ProductId, g.Quantity)).ToList();
        var promotion = Promotion.Create(
            "Gift test", null, PromotionType.Gift, 0m, null, minOrderSubtotal,
            startsAt ?? DateTime.UtcNow.AddDays(-1), endsAt ?? DateTime.UtcNow.AddDays(30), usageLimit, perUserLimit,
            scope, targetProductIds, targetCategoryIds, minQuantity, items).Value;
        promotion.Activate();
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();
        return promotion;
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnGrant_When_OrderScopeSubtotalMet()
    {
        var (db, evaluator, _) = BuildScope();
        var giftProductId = Guid.NewGuid();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 200000m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (giftProductId, 2) });

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 300000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 300000m));

        grants.Should().ContainSingle(g => g.PromotionId == promotion.Id && g.ProductId == giftProductId && g.Quantity == 2);
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnEmpty_When_OrderScopeSubtotalNotMet()
    {
        var (db, evaluator, _) = BuildScope();
        await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 200000m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnGrant_When_ProductsScopeMinQuantityMet()
    {
        var (db, evaluator, _) = BuildScope();
        var triggerProductId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 0m, scope: PromotionScope.Products,
            targetProductIds: new[] { triggerProductId }, targetCategoryIds: null, minQuantity: 2,
            giftItems: new[] { (giftProductId, 1) });

        var lines = new[] { new CouponLine(triggerProductId, 2, 200000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 200000m));

        grants.Should().ContainSingle(g => g.PromotionId == promotion.Id);
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnEmpty_When_ProductsScopeMinQuantityNotMet()
    {
        var (db, evaluator, _) = BuildScope();
        var triggerProductId = Guid.NewGuid();
        await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 0m, scope: PromotionScope.Products,
            targetProductIds: new[] { triggerProductId }, targetCategoryIds: null, minQuantity: 2,
            giftItems: new[] { (Guid.NewGuid(), 1) });

        var lines = new[] { new CouponLine(triggerProductId, 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnGrant_When_CategoriesScopeMatches()
    {
        var (db, evaluator, catalog) = BuildScope();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        catalog.WithProduct(productId, categoryId);

        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 0m, scope: PromotionScope.Categories,
            targetProductIds: null, targetCategoryIds: new[] { categoryId }, minQuantity: 1,
            giftItems: new[] { (Guid.NewGuid(), 1) });

        var lines = new[] { new CouponLine(productId, 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Should().ContainSingle(g => g.PromotionId == promotion.Id);
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnEmpty_When_PromotionExpired()
    {
        var (db, evaluator, _) = BuildScope();
        await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) },
            startsAt: DateTime.UtcNow.AddDays(-10), endsAt: DateTime.UtcNow.AddDays(-1));

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnEmpty_When_UsageLimitReached()
    {
        var (db, evaluator, _) = BuildScope();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) }, usageLimit: 1);
        promotion.IncrementUsage();
        db.Promotions.Update(promotion);
        await db.SaveChangesAsync();

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnEmpty_When_PerUserLimitReached()
    {
        var (db, evaluator, _) = BuildScope();
        var userId = Guid.NewGuid();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) }, perUserLimit: 1);
        db.GiftRedemptions.Add(GiftRedemption.Create(promotion.Id, Guid.NewGuid(), userId, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(userId, lines, 100000m));

        grants.Should().BeEmpty();
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_ReturnGrantsFromBothPromotions_When_BothQualify()
    {
        var (db, evaluator, _) = BuildScope();
        var promotionA = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });
        var promotionB = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Select(g => g.PromotionId).Should().BeEquivalentTo(new[] { promotionA.Id, promotionB.Id });
    }

    [Fact]
    public async Task ReserveGiftsAsync_Should_IncrementUsedCount_And_CreateRedemption()
    {
        var (db, evaluator, _) = BuildScope();
        var giftProductId = Guid.NewGuid();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (giftProductId, 1) });
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };

        var grants = await evaluator.ReserveGiftsAsync(new GiftRequest(userId, lines, 100000m), orderId, new[] { promotion.Id });

        grants.Should().ContainSingle(g => g.ProductId == giftProductId);
        var reloaded = await db.Promotions.AsNoTracking().FirstAsync(p => p.Id == promotion.Id);
        reloaded.UsedCount.Should().Be(1);
        var redemption = await db.GiftRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        redemption.Status.Should().Be(RedemptionStatus.Reserved);
    }

    [Fact]
    public async Task ReserveGiftsAsync_Should_BeIdempotent_When_CalledTwiceForSameOrder()
    {
        var (db, evaluator, _) = BuildScope();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });
        var orderId = Guid.NewGuid();
        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var request = new GiftRequest(Guid.NewGuid(), lines, 100000m);

        await evaluator.ReserveGiftsAsync(request, orderId, new[] { promotion.Id });
        await evaluator.ReserveGiftsAsync(request, orderId, new[] { promotion.Id });

        var reloaded = await db.Promotions.AsNoTracking().FirstAsync(p => p.Id == promotion.Id);
        reloaded.UsedCount.Should().Be(1);
        (await db.GiftRedemptions.CountAsync(r => r.OrderId == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task ReleaseGiftsAsync_Should_DecrementUsedCount_And_MarkReleased()
    {
        var (db, evaluator, _) = BuildScope();
        var promotion = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });
        var orderId = Guid.NewGuid();
        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        await evaluator.ReserveGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m), orderId, new[] { promotion.Id });

        await evaluator.ReleaseGiftsAsync(orderId);

        var reloaded = await db.Promotions.AsNoTracking().FirstAsync(p => p.Id == promotion.Id);
        reloaded.UsedCount.Should().Be(0);
        var redemption = await db.GiftRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        redemption.Status.Should().Be(RedemptionStatus.Released);
    }

    [Fact]
    public async Task QuoteGiftsAsync_Should_CarryVariantId_When_GiftItemTargetsASpecificVariant()
    {
        var (db, evaluator, _) = BuildScope();
        var giftProductId = Guid.NewGuid();
        var giftVariantId = Guid.NewGuid();
        var items = new[] { new GiftItemInput(giftProductId, 1, giftVariantId) };
        var promotion = Promotion.Create(
            "Gift test", null, PromotionType.Gift, 0m, null, 1m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, items).Value;
        promotion.Activate();
        db.Promotions.Add(promotion);
        await db.SaveChangesAsync();

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var grants = await evaluator.QuoteGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m));

        grants.Should().ContainSingle(g => g.ProductId == giftProductId && g.VariantId == giftVariantId);
    }

    [Fact]
    public async Task ReleaseGiftAsync_Should_ReleaseOnlyThatPromotion_When_OrderHasMultipleGiftRedemptions()
    {
        var (db, evaluator, _) = BuildScope();
        var promotionA = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });
        var promotionB = await SeedActiveGiftPromotionAsync(
            db, minOrderSubtotal: 1m, scope: PromotionScope.Order,
            targetProductIds: null, targetCategoryIds: null, minQuantity: null,
            giftItems: new[] { (Guid.NewGuid(), 1) });
        var orderId = Guid.NewGuid();
        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        await evaluator.ReserveGiftsAsync(new GiftRequest(Guid.NewGuid(), lines, 100000m), orderId, new[] { promotionA.Id, promotionB.Id });

        await evaluator.ReleaseGiftAsync(orderId, promotionA.Id);

        var redemptionA = await db.GiftRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId && r.PromotionId == promotionA.Id);
        var redemptionB = await db.GiftRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId && r.PromotionId == promotionB.Id);
        redemptionA.Status.Should().Be(RedemptionStatus.Released);
        redemptionB.Status.Should().Be(RedemptionStatus.Reserved);
    }
}

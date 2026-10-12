using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Infrastructure.Services;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Services;

public class PromotionEvaluatorTests
{
    private static PromotionsDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<PromotionsDbContext>().UseInMemoryDatabase(name).Options);

    private static PromotionEvaluator CreateEvaluator(PromotionsDbContext db, DateTimeOffset now, FakeProductCatalogReader? catalogReader = null) =>
        new(new PromotionRepository(db), new CouponCodeRepository(db), new CouponRedemptionRepository(db), db, new FakeTimeProvider(now), catalogReader ?? new FakeProductCatalogReader());

    private static CouponRequest Req(string code, Guid userId, decimal subtotal, IReadOnlyList<CouponLine>? lines = null, decimal shippingFee = 0m) =>
        new(code, userId, lines ?? Array.Empty<CouponLine>(), subtotal, shippingFee);

    private static async Task<(Promotion Promotion, CouponCode Coupon)> SeedActivePromotionWithCouponAsync(
        PromotionsDbContext db, DateTime now, decimal value = 10m, decimal? maxDiscount = null,
        decimal minOrderSubtotal = 0m, int? usageLimit = null, int? perUserLimit = null, string code = "SALE10",
        PromotionType type = PromotionType.PercentageOff, PromotionScope scope = PromotionScope.Order,
        IReadOnlyList<Guid>? targetProductIds = null, IReadOnlyList<Guid>? targetCategoryIds = null)
    {
        var promotion = Promotion.Create("Sale", null, type, value, maxDiscount, minOrderSubtotal,
            now.AddDays(-1), now.AddDays(1), usageLimit, perUserLimit, scope, targetProductIds, targetCategoryIds);
        promotion.Activate();
        await db.Promotions.AddAsync(promotion);

        var coupon = CouponCode.Create(promotion.Id, code, now);
        await db.CouponCodes.AddAsync(coupon);

        await db.SaveChangesAsync();
        return (promotion, coupon);
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_CodeDoesNotExist()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var evaluator = CreateEvaluator(db, now);

        var (quote, error) = await evaluator.QuoteAsync(Req("NOPE", Guid.NewGuid(), 100000m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá không tồn tại");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_PromotionInactive()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var promotion = Promotion.Create("Sale", null, PromotionType.PercentageOff, 10m, null, 0m,
            now.UtcDateTime.AddDays(-1), now.UtcDateTime.AddDays(1), null, null);
        await db.Promotions.AddAsync(promotion);
        var coupon = CouponCode.Create(promotion.Id, "SALE10", now.UtcDateTime);
        await db.CouponCodes.AddAsync(coupon);
        await db.SaveChangesAsync();

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá không còn hiệu lực");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_CouponInactive()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var (_, coupon) = await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime);
        coupon.Deactivate();
        await db.SaveChangesAsync();

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá không còn hiệu lực");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_NotYetStarted()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var promotion = Promotion.Create("Sale", null, PromotionType.PercentageOff, 10m, null, 0m,
            now.UtcDateTime.AddDays(1), now.UtcDateTime.AddDays(5), null, null);
        promotion.Activate();
        await db.Promotions.AddAsync(promotion);
        var coupon = CouponCode.Create(promotion.Id, "SALE10", now.UtcDateTime);
        await db.CouponCodes.AddAsync(coupon);
        await db.SaveChangesAsync();

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá chưa có hiệu lực");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_Expired()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var promotion = Promotion.Create("Sale", null, PromotionType.PercentageOff, 10m, null, 0m,
            now.UtcDateTime.AddDays(-5), now.UtcDateTime.AddDays(-1), null, null);
        promotion.Activate();
        await db.Promotions.AddAsync(promotion);
        var coupon = CouponCode.Create(promotion.Id, "SALE10", now.UtcDateTime);
        await db.CouponCodes.AddAsync(coupon);
        await db.SaveChangesAsync();

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá đã hết hạn");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_SubtotalBelowMinimum()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, minOrderSubtotal: 200000m);

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Đơn hàng chưa đạt giá trị tối thiểu 200,000");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnDiscount_When_CouponValid()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m);

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("sale10", Guid.NewGuid(), 200000m));

        error.Should().BeNull();
        quote!.Code.Should().Be("SALE10");
        quote.DiscountAmount.Should().Be(20000m);
    }

    [Fact]
    public async Task ReserveAsync_Should_IncrementUsageAndCreateRedemption_When_Valid()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        var (promotion, coupon) = await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m);

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.ReserveAsync(Req("SALE10", userId, 200000m), orderId);

        error.Should().BeNull();
        quote!.DiscountAmount.Should().Be(20000m);

        using var verifyDb = CreateDb(dbName);
        verifyDb.Promotions.Single(p => p.Id == promotion.Id).UsedCount.Should().Be(1);
        verifyDb.CouponCodes.Single(c => c.Id == coupon.Id).UsedCount.Should().Be(1);
        var redemption = verifyDb.CouponRedemptions.Single(r => r.OrderId == orderId);
        redemption.Status.Should().Be(RedemptionStatus.Reserved);
        redemption.UserId.Should().Be(userId);
        redemption.DiscountAmount.Should().Be(20000m);
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnSameQuote_When_CalledTwiceForSameOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        var (promotion, _) = await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m);

        var evaluator = CreateEvaluator(db, now);
        var first = await evaluator.ReserveAsync(Req("SALE10", userId, 200000m), orderId);
        var second = await evaluator.ReserveAsync(Req("SALE10", userId, 200000m), orderId);

        second.Error.Should().BeNull();
        second.Quote!.DiscountAmount.Should().Be(first.Quote!.DiscountAmount);

        using var verifyDb = CreateDb(dbName);
        verifyDb.Promotions.Single(p => p.Id == promotion.Id).UsedCount.Should().Be(1);
        verifyDb.CouponRedemptions.Count(r => r.OrderId == orderId).Should().Be(1);
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnError_When_UsageLimitReached()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m, usageLimit: 1);

        var evaluator = CreateEvaluator(db, now);
        var firstResult = await evaluator.ReserveAsync(Req("SALE10", Guid.NewGuid(), 100000m), Guid.NewGuid());
        firstResult.Error.Should().BeNull();

        var (quote, error) = await evaluator.ReserveAsync(Req("SALE10", Guid.NewGuid(), 100000m), Guid.NewGuid());

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá đã hết lượt sử dụng");
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnError_When_PerUserLimitReached()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m, perUserLimit: 1);

        var evaluator = CreateEvaluator(db, now);
        var firstResult = await evaluator.ReserveAsync(Req("SALE10", userId, 100000m), Guid.NewGuid());
        firstResult.Error.Should().BeNull();

        var (quote, error) = await evaluator.ReserveAsync(Req("SALE10", userId, 100000m), Guid.NewGuid());

        quote.Should().BeNull();
        error!.Message.Should().Be("Bạn đã sử dụng hết lượt cho mã này");
    }

    [Fact]
    public async Task ReserveAsync_Should_AllowDifferentUsers_When_PerUserLimitReachedByAnotherUser()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m, perUserLimit: 1);

        var evaluator = CreateEvaluator(db, now);
        var firstResult = await evaluator.ReserveAsync(Req("SALE10", Guid.NewGuid(), 100000m), Guid.NewGuid());
        firstResult.Error.Should().BeNull();

        var secondResult = await evaluator.ReserveAsync(Req("SALE10", Guid.NewGuid(), 100000m), Guid.NewGuid());
        secondResult.Error.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_Should_DecrementUsageAndMarkReleased_When_Reserved()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        var (promotion, coupon) = await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m);

        var evaluator = CreateEvaluator(db, now);
        await evaluator.ReserveAsync(Req("SALE10", Guid.NewGuid(), 100000m), orderId);

        await evaluator.ReleaseAsync(orderId);

        using var verifyDb = CreateDb(dbName);
        verifyDb.Promotions.Single(p => p.Id == promotion.Id).UsedCount.Should().Be(0);
        verifyDb.CouponCodes.Single(c => c.Id == coupon.Id).UsedCount.Should().Be(0);
        verifyDb.CouponRedemptions.Single(r => r.OrderId == orderId).Status.Should().Be(RedemptionStatus.Released);
    }

    [Fact]
    public async Task ReleaseAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        var (promotion, _) = await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m);

        var evaluator = CreateEvaluator(db, now);
        await evaluator.ReserveAsync(Req("SALE10", Guid.NewGuid(), 100000m), orderId);

        await evaluator.ReleaseAsync(orderId);
        await evaluator.ReleaseAsync(orderId);

        using var verifyDb = CreateDb(dbName);
        verifyDb.Promotions.Single(p => p.Id == promotion.Id).UsedCount.Should().Be(0);
    }

    [Fact]
    public async Task ReleaseAsync_Should_NoOp_When_NoReservationForOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);

        var evaluator = CreateEvaluator(db, now);

        await evaluator.ReleaseAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task QuoteAsync_Should_DiscountOnlyMatchingLine_When_ScopeIsProducts()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var matchingProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m, scope: PromotionScope.Products, targetProductIds: new[] { matchingProductId });

        var lines = new[]
        {
            new CouponLine(matchingProductId, 1, 100000m),
            new CouponLine(otherProductId, 1, 100000m)
        };
        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 200000m, lines));

        error.Should().BeNull();
        quote!.DiscountAmount.Should().Be(10000m);
        quote.DiscountTarget.Should().Be("Items");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnNotApplicable_When_ScopeIsProductsAndNoLineMatches()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 10m, scope: PromotionScope.Products, targetProductIds: new[] { Guid.NewGuid() });

        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 100000m) };
        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m, lines));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá không áp dụng cho sản phẩm trong giỏ");
    }

    [Fact]
    public async Task QuoteAsync_Should_DiscountOnlyMatchingCategoryLine_When_ScopeIsCategories()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var targetCategoryId = Guid.NewGuid();
        var matchingProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 20m, scope: PromotionScope.Categories, targetCategoryIds: new[] { targetCategoryId });

        var catalog = new FakeProductCatalogReader()
            .WithProduct(matchingProductId, targetCategoryId)
            .WithProduct(otherProductId, Guid.NewGuid());
        var lines = new[]
        {
            new CouponLine(matchingProductId, 1, 100000m),
            new CouponLine(otherProductId, 1, 100000m)
        };
        var evaluator = CreateEvaluator(db, now, catalog);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 200000m, lines));

        error.Should().BeNull();
        quote!.DiscountAmount.Should().Be(20000m);
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnNotApplicable_When_ScopeIsCategoriesAndNoLineMatches()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var productId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 20m, scope: PromotionScope.Categories, targetCategoryIds: new[] { Guid.NewGuid() });

        var catalog = new FakeProductCatalogReader().WithProduct(productId, Guid.NewGuid());
        var lines = new[] { new CouponLine(productId, 1, 100000m) };
        var evaluator = CreateEvaluator(db, now, catalog);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 100000m, lines));

        quote.Should().BeNull();
        error!.Message.Should().Be("Mã giảm giá không áp dụng cho sản phẩm trong giỏ");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnShippingFeeAsDiscount_When_TypeIsFreeShipping()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 0m, type: PromotionType.FreeShipping);

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 199000m, shippingFee: 30000m));

        error.Should().BeNull();
        quote!.DiscountAmount.Should().Be(30000m);
        quote.DiscountTarget.Should().Be("Shipping");
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_TypeIsFreeShippingAndOrderAlreadyShipsFree()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        await SeedActivePromotionWithCouponAsync(db, now.UtcDateTime, value: 0m, type: PromotionType.FreeShipping);

        var evaluator = CreateEvaluator(db, now);
        var (quote, error) = await evaluator.QuoteAsync(Req("SALE10", Guid.NewGuid(), 700000m, shippingFee: 0m));

        quote.Should().BeNull();
        error!.Message.Should().Be("Đơn hàng đã được miễn phí vận chuyển");
    }
}

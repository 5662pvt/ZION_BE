using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Infrastructure.Services;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Services;

/// <summary>Defensive coverage: CreateCoupon/GenerateCoupons already refuse to mint a coupon for a Gift
/// promotion, so a CouponCode can never legitimately point at one — these tests simulate that
/// otherwise-unreachable state directly against the domain to prove PromotionEvaluator still refuses it.</summary>
public class PromotionEvaluatorGiftRejectionTests
{
    private static async Task<(PromotionsDbContext Db, PromotionEvaluator Evaluator, CouponCode Coupon)> BuildScopeWithGiftCouponAsync()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var promotion = Promotion.Create(
            "Gift", null, PromotionType.Gift, 0m, null, 100000m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null,
            PromotionScope.Order, null, null, null, new[] { new GiftItemInput(Guid.NewGuid(), 1) }).Value;
        promotion.Activate();
        var coupon = CouponCode.Create(promotion.Id, "GIFTLEAK", DateTime.UtcNow);
        db.Promotions.Add(promotion);
        db.CouponCodes.Add(coupon);
        await db.SaveChangesAsync();

        var evaluator = new PromotionEvaluator(
            new PromotionRepository(db), new CouponCodeRepository(db), new CouponRedemptionRepository(db), db, TimeProvider.System, new FakeProductCatalogReader());
        return (db, evaluator, coupon);
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnError_When_PromotionIsGift()
    {
        var (_, evaluator, coupon) = await BuildScopeWithGiftCouponAsync();
        var lines = new[] { new CouponLine(Guid.NewGuid(), 1, 150000m) };

        var (quote, error) = await evaluator.QuoteAsync(new CouponRequest(coupon.Code, Guid.NewGuid(), lines, 150000m, 30000m));

        quote.Should().BeNull();
        error!.Code.Should().Be("Promotions.CouponNotAllowedForGift");
        error.Message.Should().Be("Khuyến mãi quà tặng không dùng mã giảm giá");
    }
}

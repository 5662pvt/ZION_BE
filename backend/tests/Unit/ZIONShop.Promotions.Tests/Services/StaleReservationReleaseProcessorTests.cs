using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.Services;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.Services;

public class StaleReservationReleaseProcessorTests
{
    private static PromotionsDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<PromotionsDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task ReleaseStaleReservationsAsync_Should_ReleaseOnlyReservationsOlderThan30Minutes()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var staleOrderId = Guid.NewGuid();
        var freshOrderId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            var promotion = Promotion.Create("Sale", null, ZIONShop.Promotions.Domain.Enums.PromotionType.FixedAmountOff, 1000m, null, 0m,
                now.UtcDateTime.AddDays(-1), now.UtcDateTime.AddDays(1), null, null);
            promotion.Activate();
            await seedDb.Promotions.AddAsync(promotion);
            var coupon = CouponCode.Create(promotion.Id, "SALE", now.UtcDateTime);
            await seedDb.CouponCodes.AddAsync(coupon);

            var staleRedemption = CouponRedemption.Create(promotion.Id, coupon.Id, staleOrderId, Guid.NewGuid(), 1000m, now.UtcDateTime.AddMinutes(-31));
            var freshRedemption = CouponRedemption.Create(promotion.Id, coupon.Id, freshOrderId, Guid.NewGuid(), 1000m, now.UtcDateTime.AddMinutes(-5));
            await seedDb.CouponRedemptions.AddAsync(staleRedemption);
            await seedDb.CouponRedemptions.AddAsync(freshRedemption);
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var evaluator = new Mock<IPromotionEvaluator>();
        evaluator.Setup(e => e.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var processor = new StaleReservationReleaseProcessor(
            new CouponRedemptionRepository(db), evaluator.Object, new FakeTimeProvider(now), NullLogger<StaleReservationReleaseProcessor>.Instance);

        var count = await processor.ReleaseStaleReservationsAsync();

        count.Should().Be(1);
        evaluator.Verify(e => e.ReleaseAsync(staleOrderId, It.IsAny<CancellationToken>()), Times.Once);
        evaluator.Verify(e => e.ReleaseAsync(freshOrderId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseStaleReservationsAsync_Should_ReturnZero_When_NothingStale()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var evaluator = new Mock<IPromotionEvaluator>();

        var processor = new StaleReservationReleaseProcessor(
            new CouponRedemptionRepository(db), evaluator.Object, new FakeTimeProvider(now), NullLogger<StaleReservationReleaseProcessor>.Instance);

        var count = await processor.ReleaseStaleReservationsAsync();

        count.Should().Be(0);
        evaluator.Verify(e => e.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Promotions.Application.Services;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Tests.Fakes;

namespace ZIONShop.Promotions.Tests.Services;

public class StaleReservationReleaseProcessorTests
{
    private static (PromotionsDbContext Db, StaleReservationReleaseProcessor Processor, FakePromotionEvaluator Coupons, FakeGiftPromotionEvaluator Gifts) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var coupons = new FakePromotionEvaluator();
        var gifts = new FakeGiftPromotionEvaluator();
        var processor = new StaleReservationReleaseProcessor(
            new CouponRedemptionRepository(db), new GiftRedemptionRepository(db), coupons, gifts, TimeProvider.System, NullLogger<StaleReservationReleaseProcessor>.Instance);
        return (db, processor, coupons, gifts);
    }

    [Fact]
    public async Task ReleaseStaleReservationsAsync_Should_ReleaseStaleGiftRedemption_When_OlderThanThirtyMinutes()
    {
        var (db, processor, _, gifts) = BuildScope();
        var orderId = Guid.NewGuid();
        var stale = GiftRedemption.Create(Guid.NewGuid(), orderId, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-40));
        db.GiftRedemptions.Add(stale);
        await db.SaveChangesAsync();

        var released = await processor.ReleaseStaleReservationsAsync();

        released.Should().Be(1);
        gifts.ReleasedGiftsOrderIds.Should().ContainSingle(id => id == orderId);
    }

    [Fact]
    public async Task ReleaseStaleReservationsAsync_Should_NotReleaseGiftRedemption_When_WithinThirtyMinutes()
    {
        var (db, processor, _, gifts) = BuildScope();
        var fresh = GiftRedemption.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-5));
        db.GiftRedemptions.Add(fresh);
        await db.SaveChangesAsync();

        var released = await processor.ReleaseStaleReservationsAsync();

        released.Should().Be(0);
        gifts.ReleasedGiftsOrderIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ReleaseStaleReservationsAsync_Should_ReleaseStaleCouponRedemption_When_OlderThanThirtyMinutes()
    {
        var (db, processor, coupons, _) = BuildScope();
        var orderId = Guid.NewGuid();
        var stale = CouponRedemption.Create(Guid.NewGuid(), Guid.NewGuid(), orderId, Guid.NewGuid(), 10000m, DateTime.UtcNow.AddMinutes(-45));
        db.CouponRedemptions.Add(stale);
        await db.SaveChangesAsync();

        var released = await processor.ReleaseStaleReservationsAsync();

        released.Should().Be(1);
        coupons.ReleasedOrderIds.Should().ContainSingle(id => id == orderId);
    }
}

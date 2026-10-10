using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Promotions.Application.IntegrationEvents;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;
using ZIONShop.Promotions.Tests.TestSupport;

namespace ZIONShop.Promotions.Tests.IntegrationEvents;

public class OrderConfirmedIntegrationEventHandlerTests
{
    private static PromotionsDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<PromotionsDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task HandleAsync_Should_MarkCommitted_When_Reserved()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            var promotion = Promotion.Create("Sale", null, PromotionType.FixedAmountOff, 1000m, null, 0m,
                now.UtcDateTime.AddDays(-1), now.UtcDateTime.AddDays(1), null, null);
            promotion.Activate();
            await seedDb.Promotions.AddAsync(promotion);
            var coupon = CouponCode.Create(promotion.Id, "SALE", now.UtcDateTime);
            await seedDb.CouponCodes.AddAsync(coupon);
            await seedDb.CouponRedemptions.AddAsync(CouponRedemption.Create(promotion.Id, coupon.Id, orderId, userId, 1000m, now.UtcDateTime));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(new CouponRedemptionRepository(db), db, new FakeTimeProvider(now));

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));

        using var verifyDb = CreateDb(dbName);
        verifyDb.CouponRedemptions.Single(r => r.OrderId == orderId).Status.Should().Be(RedemptionStatus.Committed);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            var promotion = Promotion.Create("Sale", null, PromotionType.FixedAmountOff, 1000m, null, 0m,
                now.UtcDateTime.AddDays(-1), now.UtcDateTime.AddDays(1), null, null);
            promotion.Activate();
            await seedDb.Promotions.AddAsync(promotion);
            var coupon = CouponCode.Create(promotion.Id, "SALE", now.UtcDateTime);
            await seedDb.CouponCodes.AddAsync(coupon);
            await seedDb.CouponRedemptions.AddAsync(CouponRedemption.Create(promotion.Id, coupon.Id, orderId, userId, 1000m, now.UtcDateTime));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(new CouponRedemptionRepository(db), db, new FakeTimeProvider(now));

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));
        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));

        using var verifyDb = CreateDb(dbName);
        verifyDb.CouponRedemptions.Single(r => r.OrderId == orderId).Status.Should().Be(RedemptionStatus.Committed);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_NoRedemptionForOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(new CouponRedemptionRepository(db), db, new FakeTimeProvider(now));

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()));
    }
}

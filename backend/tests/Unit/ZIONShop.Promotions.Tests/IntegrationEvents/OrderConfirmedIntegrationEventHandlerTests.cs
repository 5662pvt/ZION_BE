using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Promotions.Application.IntegrationEvents;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;

namespace ZIONShop.Promotions.Tests.IntegrationEvents;

public class OrderConfirmedIntegrationEventHandlerTests
{
    private static (PromotionsDbContext Db, OrderConfirmedIntegrationEventHandler Handler) BuildScope()
    {
        var options = new DbContextOptionsBuilder<PromotionsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new PromotionsDbContext(options);
        var handler = new OrderConfirmedIntegrationEventHandler(new CouponRedemptionRepository(db), new GiftRedemptionRepository(db), db, TimeProvider.System);
        return (db, handler);
    }

    [Fact]
    public async Task HandleAsync_Should_CommitAllReservedGiftRedemptions_When_OrderHasMultipleGiftPromotions()
    {
        var (db, handler) = BuildScope();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var giftA = GiftRedemption.Create(Guid.NewGuid(), orderId, userId, DateTime.UtcNow);
        var giftB = GiftRedemption.Create(Guid.NewGuid(), orderId, userId, DateTime.UtcNow);
        db.GiftRedemptions.AddRange(giftA, giftB);
        await db.SaveChangesAsync();

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId), CancellationToken.None);

        var reloaded = await db.GiftRedemptions.AsNoTracking().Where(r => r.OrderId == orderId).ToListAsync();
        reloaded.Should().OnlyContain(r => r.Status == RedemptionStatus.Committed);
    }

    [Fact]
    public async Task HandleAsync_Should_CommitCouponRedemption_When_Reserved()
    {
        var (db, handler) = BuildScope();
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var redemption = CouponRedemption.Create(Guid.NewGuid(), Guid.NewGuid(), orderId, userId, 10000m, DateTime.UtcNow);
        db.CouponRedemptions.Add(redemption);
        await db.SaveChangesAsync();

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId), CancellationToken.None);

        var reloaded = await db.CouponRedemptions.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        reloaded.Status.Should().Be(RedemptionStatus.Committed);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_NoRedemptionsForOrder()
    {
        var (_, handler) = BuildScope();

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        // No exception means success; nothing to assert beyond idempotent no-op behavior.
    }
}

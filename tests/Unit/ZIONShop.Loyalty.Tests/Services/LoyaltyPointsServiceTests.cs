using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.Repositories;
using ZIONShop.Loyalty.Infrastructure.Services;
using ZIONShop.Loyalty.Tests.Fakes;
using ZIONShop.Loyalty.Tests.TestSupport;

namespace ZIONShop.Loyalty.Tests.Services;

public class LoyaltyPointsServiceTests
{
    private static LoyaltyDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<LoyaltyDbContext>().UseInMemoryDatabase(name).Options);

    private static LoyaltyPointsService CreateService(LoyaltyDbContext db, DateTimeOffset now, LoyaltyOptions? options = null, FakeOrderDetailsReader? orderReader = null) =>
        new(
            new PointAccountRepository(db),
            new PointLotRepository(db),
            new PointTransactionRepository(db),
            new PointReservationRepository(db),
            new OrderPointsRevocationRepository(db),
            db,
            Options.Create(options ?? new LoyaltyOptions()),
            orderReader ?? new FakeOrderDetailsReader(),
            new FakeTimeProvider(now));

    [Fact]
    public async Task EarnAsync_Should_CreateLotAndIncreaseBalance_When_FirstTime()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        var earned = await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #TEST");

        earned.Should().Be(100);
        (await service.GetBalanceAsync(userId)).Should().Be(100);
    }

    [Fact]
    public async Task EarnAsync_Should_BeIdempotent_When_CalledTwiceWithSameSourceRef()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #TEST");
        var second = await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #TEST");

        second.Should().Be(0);
        (await service.GetBalanceAsync(userId)).Should().Be(100);
    }

    [Fact]
    public async Task EarnAsync_Should_AllowReviewBonusAndOrderEarn_When_SourceRefsDiffer()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #TEST");
        await service.EarnAsync(userId, 100, PointLotSource.Review, productId.ToString(), null, productId, "Đánh giá sản phẩm");

        (await service.GetBalanceAsync(userId)).Should().Be(200);
    }

    [Fact]
    public async Task EarnAsync_Should_Skip_When_RevocationMarkerAlreadyExistsForOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        // Refund arrives before the (late) OrderCompleted earn.
        await service.RevokeOrderEarnAsync(orderId, userId, "Thu hồi Xu do trả hàng đơn #TEST");

        var earned = await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #TEST");

        earned.Should().Be(0);
        (await service.GetBalanceAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task RevokeOrderEarnAsync_Should_ZeroOutLotAndDecreaseBalance_When_AlreadyEarned()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #TEST");

        await service.RevokeOrderEarnAsync(orderId, userId, "Thu hồi Xu do trả hàng đơn #TEST");

        (await service.GetBalanceAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task RevokeOrderEarnAsync_Should_NotTouchOtherLots_When_UserPartiallySpentTheEarnedLot()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.Order, orderId.ToString(), orderId, null, "Hoàn tất đơn #A");
        await service.EarnAsync(userId, 50, PointLotSource.Review, productId.ToString(), null, productId, "Đánh giá sản phẩm");

        await service.RevokeOrderEarnAsync(orderId, userId, "Thu hồi Xu do trả hàng đơn #A");

        (await service.GetBalanceAsync(userId)).Should().Be(50);
    }

    [Fact]
    public async Task QuoteAsync_Should_CapAtMaxRedeemPercent()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 1000, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");

        var usable = await service.QuoteAsync(userId, 1000m);

        usable.Should().Be(500);
    }

    [Fact]
    public async Task QuoteAsync_Should_ReturnZero_When_NoBalance()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        var usable = await service.QuoteAsync(Guid.NewGuid(), 1000m);

        usable.Should().Be(0);
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnNothing_When_BalanceIsZero()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        var (redemption, error) = await service.ReserveAsync(Guid.NewGuid(), Guid.NewGuid(), 1000m);

        redemption.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public async Task ReserveAsync_Should_ConsumePointsAndCreateReservation_When_Valid()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");

        var (redemption, error) = await service.ReserveAsync(userId, orderId, 1000m);

        error.Should().BeNull();
        redemption!.Points.Should().Be(100);
        (await service.GetBalanceAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task ReserveAsync_Should_UsePassedOrderCodeInSpendDescription_When_OrderCodeGiven()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");

        await service.ReserveAsync(userId, orderId, 1000m, "ZS261022ON8H");

        var transaction = db.PointTransactions.Single(t => t.OrderId == orderId);
        transaction.Description.Should().Be("Dùng Xu cho đơn #ZS261022ON8H");
    }

    [Fact]
    public async Task ReserveAsync_Should_ReturnSameRedemption_When_CalledTwiceForSameOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");

        var first = await service.ReserveAsync(userId, orderId, 1000m);
        var second = await service.ReserveAsync(userId, orderId, 1000m);

        second.Redemption!.Points.Should().Be(first.Redemption!.Points);
        (await service.GetBalanceAsync(userId)).Should().Be(0);
    }

    [Fact]
    public async Task ReleaseAsync_Should_GiveBackPointsToLiveLot_When_LotStillFarFromExpiry()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");
        await service.ReserveAsync(userId, orderId, 1000m);

        await service.ReleaseAsync(orderId);

        (await service.GetBalanceAsync(userId)).Should().Be(100);
        db.PointLots.Count(l => l.UserId == userId).Should().Be(1, "points should be given back to the original lot, not a new Restore lot");
    }

    [Fact]
    public async Task ReleaseAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");
        await service.ReserveAsync(userId, orderId, 1000m);

        await service.ReleaseAsync(orderId);
        await service.ReleaseAsync(orderId);

        (await service.GetBalanceAsync(userId)).Should().Be(100);
    }

    [Fact]
    public async Task ReleaseAsync_Should_CreateRestoreLot_When_OriginalLotExpiresWithinOneDay()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);

        var shortLivedLot = PointLot.Create(userId, PointLotSource.Order, Guid.NewGuid().ToString(), 100, now.UtcDateTime, now.UtcDateTime.AddHours(2));
        db.PointLots.Add(shortLivedLot);
        var account = PointAccount.Create(userId);
        account.Increase(100);
        db.PointAccounts.Add(account);
        await db.SaveChangesAsync();

        var service = CreateService(db, now);
        await service.ReserveAsync(userId, orderId, 1000m);

        await service.ReleaseAsync(orderId);

        (await service.GetBalanceAsync(userId)).Should().Be(100);
        var restoreLot = db.PointLots.Single(l => l.Source == PointLotSource.Restore);
        restoreLot.Remaining.Should().Be(100);
    }

    [Fact]
    public async Task RefundReservationAsync_Should_GiveBackCommittedReservationPoints()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");
        await service.ReserveAsync(userId, orderId, 1000m);

        var reservation = await db.PointReservations.SingleAsync(r => r.OrderId == orderId);
        reservation.MarkCommitted(now.UtcDateTime);
        await db.SaveChangesAsync();

        await service.RefundReservationAsync(orderId, "Hoàn Xu do trả hàng đơn #TEST");

        (await service.GetBalanceAsync(userId)).Should().Be(100);
    }

    [Fact]
    public async Task AdminAdjustAsync_Should_Fail_When_NegativeDeltaExceedsBalance()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 50, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");

        var result = await service.AdminAdjustAsync(userId, -100, "test", Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Loyalty.InsufficientPoints");
        (await service.GetBalanceAsync(userId)).Should().Be(50);
    }

    [Fact]
    public async Task AdminAdjustAsync_Should_IncreaseBalance_When_PositiveDelta()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);

        var result = await service.AdminAdjustAsync(userId, 500, "bonus", Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        (await service.GetBalanceAsync(userId)).Should().Be(500);
    }

    [Fact]
    public async Task AdminAdjustAsync_Should_DeductFifo_When_NegativeDeltaWithinBalance()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 500, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");

        var result = await service.AdminAdjustAsync(userId, -100, "correction", Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        (await service.GetBalanceAsync(userId)).Should().Be(400);
    }

    [Fact]
    public async Task ExpireLotAsync_Should_ZeroOutRemainingAndDecreaseBalance()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);
        var service = CreateService(db, now);
        await service.EarnAsync(userId, 100, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), null, null, "seed");
        var lot = await db.PointLots.SingleAsync(l => l.UserId == userId);

        var removed = await service.ExpireLotAsync(lot.Id);

        removed.Should().Be(100);
        (await service.GetBalanceAsync(userId)).Should().Be(0);
    }
}

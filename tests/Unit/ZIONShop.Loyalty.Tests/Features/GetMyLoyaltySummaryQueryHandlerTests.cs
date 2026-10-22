using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Loyalty.Application.Features.GetMyLoyaltySummary;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.Repositories;
using ZIONShop.Loyalty.Tests.TestSupport;

namespace ZIONShop.Loyalty.Tests.Features;

public class GetMyLoyaltySummaryQueryHandlerTests
{
    private static LoyaltyDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<LoyaltyDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task Handle_Should_ExcludeExpiredLot_When_ExpiryScanHasNotRunYet()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(Guid.NewGuid().ToString("N"));

        var expiredLot = PointLot.Create(userId, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), 100, now.UtcDateTime.AddDays(-1), now.UtcDateTime.AddMinutes(-1));
        db.PointLots.Add(expiredLot);
        var account = PointAccount.Create(userId);
        account.Increase(100);
        db.PointAccounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new GetMyLoyaltySummaryQueryHandler(
            new PointAccountRepository(db), new PointLotRepository(db), Options.Create(new LoyaltyOptions()), new FakeTimeProvider(now));

        var result = await handler.Handle(new GetMyLoyaltySummaryQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnFullBalance_When_LotNotYetExpired()
    {
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(Guid.NewGuid().ToString("N"));

        var liveLot = PointLot.Create(userId, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), 100, now.UtcDateTime, now.UtcDateTime.AddDays(30));
        db.PointLots.Add(liveLot);
        var account = PointAccount.Create(userId);
        account.Increase(100);
        db.PointAccounts.Add(account);
        await db.SaveChangesAsync();

        var handler = new GetMyLoyaltySummaryQueryHandler(
            new PointAccountRepository(db), new PointLotRepository(db), Options.Create(new LoyaltyOptions()), new FakeTimeProvider(now));

        var result = await handler.Handle(new GetMyLoyaltySummaryQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Balance.Should().Be(100);
    }
}

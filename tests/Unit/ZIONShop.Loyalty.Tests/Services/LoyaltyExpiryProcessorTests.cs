using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Loyalty.Application.Services;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.Repositories;
using ZIONShop.Loyalty.Tests.Fakes;
using ZIONShop.Loyalty.Tests.TestSupport;

namespace ZIONShop.Loyalty.Tests.Services;

public class LoyaltyExpiryProcessorTests
{
    private static LoyaltyDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<LoyaltyDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task ExpireLotsAsync_Should_ExpireOnlyBatchOfPastDueLots()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        using var db = CreateDb(dbName);

        var expiredLot = PointLot.Create(Guid.NewGuid(), PointLotSource.Order, Guid.NewGuid().ToString(), 100, now.UtcDateTime.AddMonths(-13), now.UtcDateTime.AddDays(-1));
        var activeLot = PointLot.Create(Guid.NewGuid(), PointLotSource.Order, Guid.NewGuid().ToString(), 50, now.UtcDateTime, now.UtcDateTime.AddMonths(12));
        db.PointLots.AddRange(expiredLot, activeLot);
        await db.SaveChangesAsync();

        var mutations = new FakeLoyaltyMutationService();
        var lots = new PointLotRepository(db);
        var processor = new LoyaltyExpiryProcessor(lots, mutations, new FakeTimeProvider(now), NullLogger<LoyaltyExpiryProcessor>.Instance);

        var expiredCount = await processor.ExpireLotsAsync();

        expiredCount.Should().Be(1);
    }
}

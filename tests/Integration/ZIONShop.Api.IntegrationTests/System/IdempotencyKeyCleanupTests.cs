using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Infrastructure.BackgroundServices;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

[Collection(ApiTestCollection.Name)]
public class IdempotencyKeyCleanupTests
{
    private readonly ApiFactory _factory;

    public IdempotencyKeyCleanupTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task RunOnceAsync_Should_DeleteOnlyKeysOlderThanRetention()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var oldKey = OrderIdempotencyKey.Create(Guid.NewGuid(), $"old-{Guid.NewGuid():N}", "hash", Guid.NewGuid(), DateTime.UtcNow.AddDays(-8));
        var freshKey = OrderIdempotencyKey.Create(Guid.NewGuid(), $"fresh-{Guid.NewGuid():N}", "hash", Guid.NewGuid(), DateTime.UtcNow);
        db.OrderIdempotencyKeys.AddRange(oldKey, freshKey);
        await db.SaveChangesAsync();

        var cleanup = scope.ServiceProvider.GetRequiredService<IdempotencyKeyCleanupBackgroundService>();
        await cleanup.RunOnceAsync();

        var remaining = await db.OrderIdempotencyKeys
            .Where(k => k.Id == oldKey.Id || k.Id == freshKey.Id)
            .Select(k => k.Id)
            .ToListAsync();

        remaining.Should().ContainSingle().Which.Should().Be(freshKey.Id);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Inventory.Application.Services;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;
using ZIONShop.Inventory.Tests.TestSupport;

namespace ZIONShop.Inventory.Tests.Services;

public class ReservationExpiryProcessorTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task ExpireDueReservationsAsync_Should_ReleaseAndMarkExpired_When_PastExpiry()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiredAt = now.UtcDateTime.AddMinutes(-1);

        using (var seedDb = CreateDb(dbName))
        {
            var seedItem = InventoryItem.Create(productId, "SKU-A", "Product A", 10);
            seedItem.Reserve(3);
            await seedDb.InventoryItems.AddAsync(seedItem);
            await seedDb.StockReservations.AddAsync(StockReservation.Create(orderId, productId, 3, expiredAt, now.UtcDateTime.AddMinutes(-16)));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var events = new List<ZIONShop.EventBus.Abstractions.IntegrationEvent>();
        var processor = new ReservationExpiryProcessor(
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeInventoryOutbox(events), new FakeTimeProvider(now), NullLogger<ReservationExpiryProcessor>.Instance);

        var expiredCount = await processor.ExpireDueReservationsAsync();

        expiredCount.Should().Be(1);

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.Reserved.Should().Be(0);
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).Status.Should().Be(ReservationStatus.Expired);

        events.OfType<InventoryReservationExpiredIntegrationEvent>().Should().ContainSingle(e => e.OrderId == orderId);
    }

    [Fact]
    public async Task ExpireDueReservationsAsync_Should_Ignore_When_NotYetDue()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var seedDb = CreateDb(dbName))
        {
            var seedItem = InventoryItem.Create(productId, "SKU-A", "Product A", 10);
            seedItem.Reserve(3);
            await seedDb.InventoryItems.AddAsync(seedItem);
            await seedDb.StockReservations.AddAsync(StockReservation.Create(orderId, productId, 3, now.UtcDateTime.AddMinutes(10), now.UtcDateTime));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var events = new List<ZIONShop.EventBus.Abstractions.IntegrationEvent>();
        var processor = new ReservationExpiryProcessor(
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeInventoryOutbox(events), new FakeTimeProvider(now), NullLogger<ReservationExpiryProcessor>.Instance);

        var expiredCount = await processor.ExpireDueReservationsAsync();

        expiredCount.Should().Be(0);
        events.Should().BeEmpty();
    }
}

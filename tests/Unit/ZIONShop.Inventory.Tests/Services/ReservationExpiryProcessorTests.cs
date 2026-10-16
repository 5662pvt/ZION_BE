using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    // The processor now opens an explicit transaction (see ReservationExpiryProcessor.ExpireDueReservationsAsync)
    // -- InMemory doesn't support real transactions and otherwise escalates that into a thrown warning.
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>()
        .UseInMemoryDatabase(name)
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

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
            new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
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
            new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeInventoryOutbox(events), new FakeTimeProvider(now), NullLogger<ReservationExpiryProcessor>.Instance);

        var expiredCount = await processor.ExpireDueReservationsAsync();

        expiredCount.Should().Be(0);
        events.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpireDueReservationsAsync_Should_ReleaseWholeBatch_OrderedByProductId_AcrossOrders()
    {
        var productLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var productHigh = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var orderA = Guid.NewGuid();
        var orderB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var items = new FakeInventoryItemRepository();
        items.Seed(
            InventoryItem.Create(productHigh, "SKU-H", "Product High", 10),
            InventoryItem.Create(productLow, "SKU-L", "Product Low", 10));
        items.Get(productHigh)!.Reserve(2);
        items.Get(productLow)!.Reserve(3);

        var recorder = new RecordingInventoryItemRepository(items);
        var reservations = new FakeStockReservationRepository();
        // orderA (productHigh) expires earlier than orderB (productLow), so GetActiveExpiredAsync (sorted
        // by ExpiresAt) hands them back high-id-first -- the processor must still release low-id-first.
        await reservations.AddAsync(StockReservation.Create(orderA, productHigh, 2, now.UtcDateTime.AddMinutes(-2), now.UtcDateTime.AddMinutes(-20)));
        await reservations.AddAsync(StockReservation.Create(orderB, productLow, 3, now.UtcDateTime.AddMinutes(-1), now.UtcDateTime.AddMinutes(-20)));

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var outboxEvents = new List<ZIONShop.EventBus.Abstractions.IntegrationEvent>();
        var processor = new ReservationExpiryProcessor(
            reservations, recorder, movements, uow, new FakeInventoryOutbox(outboxEvents), new FakeTimeProvider(now), NullLogger<ReservationExpiryProcessor>.Instance);

        var expiredCount = await processor.ExpireDueReservationsAsync();

        expiredCount.Should().Be(2);
        recorder.Calls.Select(c => c.ProductId).Should().Equal(productLow, productHigh);
        recorder.Calls.Select(c => c.Operation).Should().Equal("Release", "Release");
        uow.BeginTransactionCallCount.Should().Be(1);
        outboxEvents.OfType<InventoryReservationExpiredIntegrationEvent>().Select(e => e.OrderId).Should().BeEquivalentTo(new[] { orderA, orderB });
    }
}

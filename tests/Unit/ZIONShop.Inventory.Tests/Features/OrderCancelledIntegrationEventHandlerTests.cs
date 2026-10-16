using ZIONShop.Contracts.Orders;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;
using ZIONShop.Inventory.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ZIONShop.Inventory.Tests.Features;

public class OrderCancelledIntegrationEventHandlerTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task HandleAsync_Should_ReleaseActiveReservations_When_OrderCancelled()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            var seedItem = InventoryItem.Create(productId, "SKU-A", "Product A", 10);
            seedItem.Reserve(4);
            await seedDb.InventoryItems.AddAsync(seedItem);
            await seedDb.StockReservations.AddAsync(StockReservation.Create(orderId, productId, 4, null, DateTime.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = new OrderCancelledIntegrationEventHandler(
            new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer changed mind"), CancellationToken.None);

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnCommittedStockToOnHand_When_ConfirmedCodOrderIsCancelled()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            // Simulates a COD order already confirmed: OrderConfirmedIntegrationEventHandler committed the
            // reservation, which moves stock out of OnHand and clears Reserved.
            var seedItem = InventoryItem.Create(productId, "SKU-A", "Product A", 10);
            seedItem.Reserve(4);
            seedItem.Commit(4);
            await seedDb.InventoryItems.AddAsync(seedItem);
            var reservation = StockReservation.Create(orderId, productId, 4, null, DateTime.UtcNow);
            reservation.MarkCommitted();
            await seedDb.StockReservations.AddAsync(reservation);
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = new OrderCancelledIntegrationEventHandler(
            new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer cancelled"), CancellationToken.None);

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).Status.Should().Be(ReservationStatus.Released);
        verifyDb.StockMovements.Single(m => m.ProductId == productId && m.Type == StockMovementType.Return).Quantity.Should().Be(4);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwiceForSameCancelledOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            var seedItem = InventoryItem.Create(productId, "SKU-A", "Product A", 10);
            seedItem.Reserve(4);
            seedItem.Commit(4);
            await seedDb.InventoryItems.AddAsync(seedItem);
            var reservation = StockReservation.Create(orderId, productId, 4, null, DateTime.UtcNow);
            reservation.MarkCommitted();
            await seedDb.StockReservations.AddAsync(reservation);
            await seedDb.SaveChangesAsync();
        }

        var @event = new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer cancelled");

        using (var db = CreateDb(dbName))
        {
            var handler = new OrderCancelledIntegrationEventHandler(
                new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        using (var db = CreateDb(dbName))
        {
            var handler = new OrderCancelledIntegrationEventHandler(
                new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_Should_ProcessActiveAndCommittedReservations_OrderedByProductId()
    {
        // One order can carry both an Active and a Committed reservation at once (e.g. OrderConfirmed
        // committed one product's reservation but failed/skipped another's). Both halves must be processed
        // in ONE pass ordered by ProductId -- not release-all-active-then-return-all-committed -- otherwise
        // two concurrent cancels could still take their row locks in opposite order and deadlock.
        var productLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var productHigh = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(
            InventoryItem.Create(productHigh, "SKU-H", "Product High", 10),
            InventoryItem.Create(productLow, "SKU-L", "Product Low", 10));
        items.Get(productHigh)!.Reserve(3); // still Active
        items.Get(productLow)!.Reserve(5);
        items.Get(productLow)!.Commit(5); // already Committed

        var recorder = new RecordingInventoryItemRepository(items);
        var reservations = new FakeStockReservationRepository();
        // Submitted high-id-first (Active) then low-id-first (Committed) -- the handler must still issue
        // its Try*Async calls low-id-first regardless of this input order.
        var activeReservation = StockReservation.Create(orderId, productHigh, 3, null, DateTime.UtcNow);
        await reservations.AddAsync(activeReservation);
        var committedReservation = StockReservation.Create(orderId, productLow, 5, null, DateTime.UtcNow);
        committedReservation.MarkCommitted();
        await reservations.AddAsync(committedReservation);

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var handler = new OrderCancelledIntegrationEventHandler(reservations, recorder, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow));

        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Customer cancelled"), CancellationToken.None);

        recorder.Calls.Select(c => c.ProductId).Should().Equal(productLow, productHigh);
        recorder.Calls.Select(c => c.Operation).Should().Equal("Return", "Release");

        items.Get(productHigh)!.Reserved.Should().Be(0);
        items.Get(productHigh)!.OnHand.Should().Be(10);
        items.Get(productLow)!.OnHand.Should().Be(10);
        items.Get(productLow)!.Reserved.Should().Be(0);

        reservations.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Released);
        movements.Movements.Single(m => m.ProductId == productHigh).Type.Should().Be(StockMovementType.Release);
        movements.Movements.Single(m => m.ProductId == productLow).Type.Should().Be(StockMovementType.Return);
    }
}

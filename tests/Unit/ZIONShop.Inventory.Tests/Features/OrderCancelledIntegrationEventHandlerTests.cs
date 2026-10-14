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
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
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
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
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
                new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        using (var db = CreateDb(dbName))
        {
            var handler = new OrderCancelledIntegrationEventHandler(
                new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
    }
}

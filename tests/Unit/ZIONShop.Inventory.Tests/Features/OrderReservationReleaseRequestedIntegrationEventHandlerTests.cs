using ZIONShop.Contracts.Orders;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;
using ZIONShop.Inventory.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ZIONShop.Inventory.Tests.Features;

public class OrderReservationReleaseRequestedIntegrationEventHandlerTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task HandleAsync_Should_ReleaseActiveReservation_When_OrderAlreadyCancelled()
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
        var handler = new OrderReservationReleaseRequestedIntegrationEventHandler(
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        await handler.HandleAsync(new OrderReservationReleaseRequestedIntegrationEvent(orderId, "Order already cancelled"), CancellationToken.None);

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).Status.Should().Be(ReservationStatus.Released);
        verifyDb.StockMovements.Single(m => m.ProductId == productId && m.Type == StockMovementType.Release).Quantity.Should().Be(4);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_NoActiveReservationsForOrder()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateDb(dbName);
        var handler = new OrderReservationReleaseRequestedIntegrationEventHandler(
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow));

        await handler.HandleAsync(new OrderReservationReleaseRequestedIntegrationEvent(Guid.NewGuid(), "Order already cancelled"), CancellationToken.None);
        // no exception thrown is the assertion
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwiceForSameOrder()
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

        var @event = new OrderReservationReleaseRequestedIntegrationEvent(orderId, "Order already cancelled");

        using (var db = CreateDb(dbName))
        {
            var handler = new OrderReservationReleaseRequestedIntegrationEventHandler(
                new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
                new FakeTimeProvider(DateTimeOffset.UtcNow));
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        using (var db = CreateDb(dbName))
        {
            var handler = new OrderReservationReleaseRequestedIntegrationEventHandler(
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

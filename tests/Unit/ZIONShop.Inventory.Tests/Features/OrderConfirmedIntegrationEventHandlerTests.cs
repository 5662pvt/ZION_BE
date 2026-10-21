using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Orders;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;
using ZIONShop.Inventory.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace ZIONShop.Inventory.Tests.Features;

public class OrderConfirmedIntegrationEventHandlerTests
{
    private static InventoryDbContext CreateDb(string name) => new(new DbContextOptionsBuilder<InventoryDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task HandleAsync_Should_CommitActiveReservations_When_OrderConfirmed()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using (var seedDb = CreateDb(dbName))
        {
            var seedItem = InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 10);
            seedItem.Reserve(4);
            await seedDb.InventoryItems.AddAsync(seedItem);
            await seedDb.StockReservations.AddAsync(StockReservation.Create(orderId, productId, productId, 4, null, DateTime.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(
            new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<OrderConfirmedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, Guid.NewGuid()), CancellationToken.None);

        using var verifyDb = CreateDb(dbName);
        var item = verifyDb.InventoryItems.Single(i => i.ProductId == productId);
        item.OnHand.Should().Be(6);
        item.Reserved.Should().Be(0);
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).Status.Should().Be(ReservationStatus.Committed);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_NoActiveReservations()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(
            new StockReservationRepository(db), new InMemoryInventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<OrderConfirmedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        // No exception, nothing to commit — call succeeds as a no-op.
    }

    [Fact]
    public async Task HandleAsync_Should_CommitReservations_OrderedByProductId()
    {
        var productLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var productHigh = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(
            InventoryItem.Create(productHigh, productHigh, "SKU-H", "Product High", null, 10),
            InventoryItem.Create(productLow, productLow, "SKU-L", "Product Low", null, 10));
        items.Get(productHigh)!.Reserve(2);
        items.Get(productLow)!.Reserve(3);

        var recorder = new RecordingInventoryItemRepository(items);
        var reservations = new FakeStockReservationRepository();
        // Submitted high-id-first -- the handler must still commit low-id-first.
        await reservations.AddAsync(StockReservation.Create(orderId, productHigh, productHigh, 2, null, DateTime.UtcNow));
        await reservations.AddAsync(StockReservation.Create(orderId, productLow, productLow, 3, null, DateTime.UtcNow));

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var handler = new OrderConfirmedIntegrationEventHandler(
            reservations, recorder, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<OrderConfirmedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, Guid.NewGuid()), CancellationToken.None);

        recorder.Calls.Select(c => c.VariantId).Should().Equal(productLow, productHigh);
        recorder.Calls.Select(c => c.Operation).Should().Equal("Commit", "Commit");
    }

    [Fact]
    public async Task HandleAsync_Should_LeaveReservationActive_When_CommitFails()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 10));
        items.Get(productId)!.Reserve(4);
        items.ForceCommitFailure.Add(productId); // simulate a concurrency/invariant failure on commit

        var reservations = new FakeStockReservationRepository();
        await reservations.AddAsync(StockReservation.Create(orderId, productId, productId, 4, null, DateTime.UtcNow));

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var handler = new OrderConfirmedIntegrationEventHandler(
            reservations, items, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<OrderConfirmedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, Guid.NewGuid()), CancellationToken.None);

        reservations.Reservations.Single(r => r.OrderId == orderId).Status.Should().Be(ReservationStatus.Active);
        items.Get(productId)!.Reserved.Should().Be(4); // unchanged
        movements.Movements.Should().BeEmpty();
    }
}

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
            var seedItem = InventoryItem.Create(productId, "SKU-A", "Product A", 10);
            seedItem.Reserve(4);
            await seedDb.InventoryItems.AddAsync(seedItem);
            await seedDb.StockReservations.AddAsync(StockReservation.Create(orderId, productId, 4, null, DateTime.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
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
            new StockReservationRepository(db), new InventoryItemRepository(db), new StockMovementRepository(db), db,
            new FakeTimeProvider(DateTimeOffset.UtcNow), NullLogger<OrderConfirmedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);
        // No exception, nothing to commit — call succeeds as a no-op.
    }
}

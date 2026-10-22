using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Loyalty.Application.IntegrationEvents;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using ZIONShop.Loyalty.Infrastructure.Repositories;

namespace ZIONShop.Loyalty.Tests.IntegrationEvents;

public class OrderConfirmedIntegrationEventHandlerTests
{
    private static LoyaltyDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<LoyaltyDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task HandleAsync_Should_MarkReservationCommitted_When_Reserved()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var db = CreateDb(dbName);
        var reservation = PointReservation.Create(orderId, userId, 100, 100m, Array.Empty<(Guid, int)>(), DateTime.UtcNow);
        db.PointReservations.Add(reservation);
        await db.SaveChangesAsync();

        var handler = new OrderConfirmedIntegrationEventHandler(new PointReservationRepository(db), db, TimeProvider.System);
        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, userId));

        var updated = await db.PointReservations.SingleAsync(r => r.OrderId == orderId);
        updated.Status.Should().Be(PointReservationStatus.Committed);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_NoReservationExists()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var db = CreateDb(dbName);
        var handler = new OrderConfirmedIntegrationEventHandler(new PointReservationRepository(db), db, TimeProvider.System);

        await handler.HandleAsync(new OrderConfirmedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid()));
    }
}

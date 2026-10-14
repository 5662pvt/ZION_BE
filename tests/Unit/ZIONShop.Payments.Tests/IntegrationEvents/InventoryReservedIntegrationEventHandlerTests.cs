using ZIONShop.Contracts.Inventory;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.IntegrationEvents;

public class InventoryReservedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_MarkPayable_When_ProjectionIsAwaitingStock()
    {
        var (db, _, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        await payableOrders.AddAsync(PayableOrder.Create(orderId, Guid.NewGuid(), 100000m, "VND", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var handler = new InventoryReservedIntegrationEventHandler(payableOrders, uow, TimeProvider.System);
        await handler.HandleAsync(new InventoryReservedIntegrationEvent(orderId, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15)));

        var projection = await payableOrders.GetByOrderIdAsync(orderId);
        projection!.State.Should().Be(PayableOrderState.Payable);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_ProjectionDoesNotExist()
    {
        var (db, _, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var handler = new InventoryReservedIntegrationEventHandler(payableOrders, uow, TimeProvider.System);

        var act = async () => await handler.HandleAsync(new InventoryReservedIntegrationEvent(Guid.NewGuid(), Array.Empty<ReservedItem>(), DateTime.UtcNow));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, _, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        await payableOrders.AddAsync(PayableOrder.Create(orderId, Guid.NewGuid(), 100000m, "VND", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var handler = new InventoryReservedIntegrationEventHandler(payableOrders, uow, TimeProvider.System);
        var @event = new InventoryReservedIntegrationEvent(orderId, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15));

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        var projection = await payableOrders.GetByOrderIdAsync(orderId);
        projection!.State.Should().Be(PayableOrderState.Payable);
    }
}

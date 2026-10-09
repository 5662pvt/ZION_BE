using ZIONShop.Contracts.Inventory;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.IntegrationEvents;

public class InventoryReservationFailedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CancelAwaitingStockProjection()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await payableOrders.AddAsync(PayableOrder.Create(orderId, userId, 100000m, "VND", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var handler = new InventoryReservationFailedIntegrationEventHandler(payableOrders, payments, uow, TimeProvider.System);
        await handler.HandleAsync(new InventoryReservationFailedIntegrationEvent(orderId, "Hết hàng", new[] { Guid.NewGuid() }));

        (await payableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Cancelled);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_NoProjectionExists()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var handler = new InventoryReservationFailedIntegrationEventHandler(payableOrders, payments, uow, TimeProvider.System);

        var act = async () => await handler.HandleAsync(new InventoryReservationFailedIntegrationEvent(Guid.NewGuid(), "Hết hàng", Array.Empty<Guid>()));

        await act.Should().NotThrowAsync();
    }
}

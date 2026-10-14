using ZIONShop.Contracts.Inventory;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.IntegrationEvents;

public class InventoryReservationExpiredIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CancelProjection_And_PendingPayment()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await payableOrders.AddAsync(PayableOrder.Create(orderId, userId, 100000m, "VND", DateTime.UtcNow));
        var payment = Payment.Create(orderId, userId, 100000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        await payments.AddAsync(payment);
        await db.SaveChangesAsync();

        var handler = new InventoryReservationExpiredIntegrationEventHandler(payableOrders, payments, uow, TimeProvider.System);
        await handler.HandleAsync(new InventoryReservationExpiredIntegrationEvent(orderId));

        (await payableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Cancelled);
        (await payments.GetByIdAsync(payment.Id))!.Status.Should().Be(PaymentStatus.Cancelled);
    }
}

using ZIONShop.Contracts.Orders;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.IntegrationEvents;

public class OrderCancelledIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CancelProjection_And_PendingPayment()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 100000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);

        var payment = Payment.Create(orderId, userId, 100000m, "VND", "Fake", "key-1", DateTime.UtcNow);
        payment.AssignIntent("fake_" + Guid.NewGuid().ToString("N")[..24]);
        await payments.AddAsync(payment);
        await db.SaveChangesAsync();

        var handler = new OrderCancelledIntegrationEventHandler(payableOrders, payments, uow, TimeProvider.System);
        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, userId, "Khách hủy đơn"));

        (await payableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Cancelled);
        (await payments.GetByIdAsync(payment.Id))!.Status.Should().Be(PaymentStatus.Cancelled);
    }

    [Fact]
    public async Task HandleAsync_Should_NotCancelPayment_When_AlreadySucceeded()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var projection = PayableOrder.Create(orderId, userId, 100000m, "VND", DateTime.UtcNow);
        projection.MarkPayable(DateTime.UtcNow);
        projection.MarkPaid(DateTime.UtcNow);
        await payableOrders.AddAsync(projection);
        await db.SaveChangesAsync();

        var handler = new OrderCancelledIntegrationEventHandler(payableOrders, payments, uow, TimeProvider.System);
        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, userId, "too late"));

        (await payableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Paid);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, payments, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await payableOrders.AddAsync(PayableOrder.Create(orderId, userId, 100000m, "VND", DateTime.UtcNow));
        await db.SaveChangesAsync();

        var handler = new OrderCancelledIntegrationEventHandler(payableOrders, payments, uow, TimeProvider.System);
        var @event = new OrderCancelledIntegrationEvent(orderId, userId, "Khách hủy đơn");

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        (await payableOrders.GetByOrderIdAsync(orderId))!.State.Should().Be(PayableOrderState.Cancelled);
    }
}

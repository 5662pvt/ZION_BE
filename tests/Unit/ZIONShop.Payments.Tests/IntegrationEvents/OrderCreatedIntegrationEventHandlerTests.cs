using ZIONShop.Contracts.Orders;
using ZIONShop.Payments.Application.IntegrationEvents;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.IntegrationEvents;

public class OrderCreatedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_CreateAwaitingStockProjection_When_PaymentMethodIsOnline()
    {
        var (db, _, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var handler = new OrderCreatedIntegrationEventHandler(payableOrders, uow, TimeProvider.System);
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new OrderCreatedIntegrationEvent(orderId, userId, 150000m, Array.Empty<OrderLine>(), "Online", "VND"));

        var projection = await payableOrders.GetByOrderIdAsync(orderId);
        projection.Should().NotBeNull();
        projection!.State.Should().Be(PayableOrderState.AwaitingStock);
        projection.UserId.Should().Be(userId);
        projection.Amount.Should().Be(150000m);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateNoProjection_When_PaymentMethodIsCod()
    {
        var (db, _, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var handler = new OrderCreatedIntegrationEventHandler(payableOrders, uow, TimeProvider.System);
        var orderId = Guid.NewGuid();

        await handler.HandleAsync(new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), 150000m, Array.Empty<OrderLine>(), "COD", "VND"));

        (await payableOrders.GetByOrderIdAsync(orderId)).Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, _, payableOrders, _) = PaymentsTestFactory.BuildScope();
        var uow = new FakePaymentsUnitOfWork(db);
        var handler = new OrderCreatedIntegrationEventHandler(payableOrders, uow, TimeProvider.System);
        var @event = new OrderCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), 150000m, Array.Empty<OrderLine>(), "Online", "VND");

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        db.PayableOrders.Count(p => p.OrderId == @event.OrderId).Should().Be(1);
    }
}

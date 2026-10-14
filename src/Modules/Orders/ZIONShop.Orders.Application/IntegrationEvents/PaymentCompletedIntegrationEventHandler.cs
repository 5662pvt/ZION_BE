using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Payments;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Application.IntegrationEvents;

public class PaymentCompletedIntegrationEventHandler : IIntegrationEventHandler<PaymentCompletedIntegrationEvent>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;

    public PaymentCompletedIntegrationEventHandler(IOrderRepository orders, IOrdersUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task HandleAsync(PaymentCompletedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(@event.OrderId, cancellationToken);
        if (order is null) return;

        var result = order.CompletePayment();
        if (result.IsFailure) return; // not AwaitingPayment anymore -> already processed, idempotent no-op

        _uow.AddOutboxEvent(new OrderConfirmedIntegrationEvent(order.Id, order.UserId));

        _orders.Update(order);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

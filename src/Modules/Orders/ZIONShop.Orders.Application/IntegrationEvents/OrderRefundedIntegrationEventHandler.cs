using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Application.IntegrationEvents;

public class OrderRefundedIntegrationEventHandler : IIntegrationEventHandler<OrderRefundedIntegrationEvent>
{
    private readonly IReturnRequestRepository _returns;
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderRefundedIntegrationEventHandler(IReturnRequestRepository returns, IOrderRepository orders, IOrdersUnitOfWork uow, TimeProvider timeProvider)
    {
        _returns = returns;
        _orders = orders;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(OrderRefundedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var returnRequest = await _returns.GetByIdAsync(@event.ReturnRequestId, cancellationToken);
        if (returnRequest is null) return;
        if (returnRequest.Status == ReturnRequestStatus.Refunded) return; // already processed, idempotent no-op

        var refundResult = returnRequest.MarkRefunded(@event.Method, _timeProvider.GetUtcNow().UtcDateTime);
        if (refundResult.IsFailure) return; // not Received anymore -> already processed, idempotent no-op

        var order = await _orders.GetByIdAsync(@event.OrderId, cancellationToken);
        if (order is not null)
        {
            var orderResult = order.MarkReturned();
            if (orderResult.IsSuccess) _orders.Update(order);
        }

        _returns.Update(returnRequest);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Repositories;

namespace ZIONShop.Payments.Application.IntegrationEvents;

/// <summary>Only Online orders are ever payable through this module; COD orders never get a projection row.</summary>
public class OrderCreatedIntegrationEventHandler : IIntegrationEventHandler<OrderCreatedIntegrationEvent>
{
    private readonly IPayableOrderRepository _payableOrders;
    private readonly IPaymentsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderCreatedIntegrationEventHandler(IPayableOrderRepository payableOrders, IPaymentsUnitOfWork uow, TimeProvider timeProvider)
    {
        _payableOrders = payableOrders;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(OrderCreatedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(@event.PaymentMethod, "Online", StringComparison.OrdinalIgnoreCase)) return;

        var existing = await _payableOrders.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        if (existing is not null) return; // idempotent: already projected

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var payableOrder = PayableOrder.Create(@event.OrderId, @event.UserId, @event.TotalAmount, @event.Currency, now);
        await _payableOrders.AddAsync(payableOrder, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

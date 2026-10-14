using ZIONShop.Contracts.Inventory;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Repositories;

namespace ZIONShop.Payments.Application.IntegrationEvents;

public class InventoryReservationExpiredIntegrationEventHandler : IIntegrationEventHandler<InventoryReservationExpiredIntegrationEvent>
{
    private readonly IPayableOrderRepository _payableOrders;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public InventoryReservationExpiredIntegrationEventHandler(
        IPayableOrderRepository payableOrders, IPaymentRepository payments, IPaymentsUnitOfWork uow, TimeProvider timeProvider)
    {
        _payableOrders = payableOrders;
        _payments = payments;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(InventoryReservationExpiredIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = await CancelPayableOrderHelper.CancelAsync(@event.OrderId, _payableOrders, _payments, now, cancellationToken);
        if (changed) await _uow.SaveChangesAsync(cancellationToken);
    }
}

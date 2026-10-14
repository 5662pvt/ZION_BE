using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Application.IntegrationEvents;

/// <summary>
/// Cancels the order when stock reservation fails, then publishes <c>OrderCancelledIntegrationEvent</c> in the
/// same SaveChanges so Notifications can email the customer. Inventory/Payments/Promotions already react to
/// <see cref="InventoryReservationFailedIntegrationEvent"/> directly (nothing was ever reserved/paid for them to
/// undo), and their <c>OrderCancelledIntegrationEvent</c> handlers are all guarded to be no-ops when there is
/// nothing left to release — so this second event is safe even though some modules now see both.
/// </summary>
public class InventoryReservationFailedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservationFailedIntegrationEvent>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;

    public InventoryReservationFailedIntegrationEventHandler(IOrderRepository orders, IOrdersUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task HandleAsync(InventoryReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(@event.OrderId, cancellationToken);
        if (order is null) return;

        var result = order.FailReservation("Hết hàng");
        if (result.IsFailure) return; // not PendingStock anymore -> already processed, idempotent no-op

        _orders.Update(order);
        _uow.AddOutboxEvent(new OrderCancelledIntegrationEvent(order.Id, order.UserId, order.CancelReason!));
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

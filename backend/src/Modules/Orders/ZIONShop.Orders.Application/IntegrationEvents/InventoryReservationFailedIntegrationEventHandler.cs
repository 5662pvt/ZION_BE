using ZIONShop.Contracts.Inventory;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Application.IntegrationEvents;

/// <summary>
/// Cancels the order when stock reservation fails. No <c>OrderCancelledIntegrationEvent</c> is published here:
/// nothing was ever reserved (no compensation needed), so there is nothing for other modules to react to at W4.
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
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

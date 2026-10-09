using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Repositories;

namespace ZIONShop.Orders.Application.IntegrationEvents;

public class InventoryReservedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservedIntegrationEvent>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;

    public InventoryReservedIntegrationEventHandler(IOrderRepository orders, IOrdersUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task HandleAsync(InventoryReservedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(@event.OrderId, cancellationToken);
        if (order is null) return;

        var result = order.ConfirmReservation();
        if (result.IsFailure) return; // not PendingStock anymore -> already processed, idempotent no-op

        if (order.Status == OrderStatus.Confirmed)
            _uow.AddOutboxEvent(new OrderConfirmedIntegrationEvent(order.Id, order.UserId));

        _orders.Update(order);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

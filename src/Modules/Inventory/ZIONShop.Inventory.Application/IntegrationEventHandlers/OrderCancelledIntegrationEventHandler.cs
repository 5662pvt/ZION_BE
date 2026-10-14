using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

public class OrderCancelledIntegrationEventHandler : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    private readonly IStockReservationRepository _reservations;
    private readonly IInventoryItemRepository _items;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderCancelledIntegrationEventHandler(
        IStockReservationRepository reservations,
        IInventoryItemRepository items,
        IStockMovementRepository movements,
        IInventoryUnitOfWork uow,
        TimeProvider timeProvider)
    {
        _reservations = reservations;
        _items = items;
        _movements = movements;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var reservations = await _reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        var active = reservations.Where(r => r.Status == ReservationStatus.Active).ToList();
        // COD stock is committed (moved out of OnHand) on OrderConfirmed, before shipping — a later
        // cancellation must give that committed stock back, not just drop the (already-cleared) reservation.
        var committed = reservations.Where(r => r.Status == ReservationStatus.Committed).ToList();
        if (active.Count == 0 && committed.Count == 0) return; // idempotent: nothing left to release or return

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await ActiveReservationReleaser.ReleaseAsync(active, _items, _movements, @event.OrderId, now, cancellationToken);

        foreach (var reservation in committed)
        {
            var item = await _items.GetByProductIdAsync(reservation.ProductId, cancellationToken);
            if (item is not null)
            {
                item.Return(reservation.Quantity);
                await _movements.AddAsync(StockMovement.Create(reservation.ProductId, StockMovementType.Return, reservation.Quantity, @event.OrderId.ToString(), now), cancellationToken);
            }

            reservation.MarkReleased();
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

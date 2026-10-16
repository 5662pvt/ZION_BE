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
        // COD stock is committed (moved out of OnHand) on OrderConfirmed, before shipping — a later
        // cancellation must give that committed stock back, not just drop the (already-cleared) reservation.
        // Active and committed reservations are processed together in ONE pass ordered by ProductId (rather
        // than two separate release-then-return passes) so two concurrent cancels/reserves/commits touching
        // the same products always take their row locks in the same order and never deadlock -- splitting
        // into two ordered-but-sequential phases would not be enough, since one order's active lines and
        // another order's committed lines for the same products could still interleave in opposite order
        // across the two phases.
        var toProcess = reservations
            .Where(r => r.Status is ReservationStatus.Active or ReservationStatus.Committed)
            .OrderBy(r => r.ProductId)
            .ToList();
        if (toProcess.Count == 0) return; // idempotent: nothing left to release or return

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var reservation in toProcess)
        {
            if (reservation.Status == ReservationStatus.Active)
            {
                if (await _items.TryReleaseAsync(reservation.ProductId, reservation.Quantity, cancellationToken))
                {
                    await _movements.AddAsync(StockMovement.Create(reservation.ProductId, StockMovementType.Release, reservation.Quantity, @event.OrderId.ToString(), now), cancellationToken);
                }
            }
            else
            {
                if (await _items.TryReturnAsync(reservation.ProductId, reservation.Quantity, cancellationToken))
                {
                    await _movements.AddAsync(StockMovement.Create(reservation.ProductId, StockMovementType.Return, reservation.Quantity, @event.OrderId.ToString(), now), cancellationToken);
                }
            }

            reservation.MarkReleased();
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

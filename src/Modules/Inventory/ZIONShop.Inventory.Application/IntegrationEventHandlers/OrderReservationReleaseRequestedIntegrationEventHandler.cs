using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

/// <summary>
/// Releases an order's active reservations on request from Orders (see
/// <see cref="OrderReservationReleaseRequestedIntegrationEvent"/>) -- the same active-reservation release path
/// as <see cref="OrderCancelledIntegrationEventHandler"/>, for stock that got reserved for an order that was
/// already cancelled by the time the reservation landed, and so will never be committed or cancelled again.
/// </summary>
public class OrderReservationReleaseRequestedIntegrationEventHandler : IIntegrationEventHandler<OrderReservationReleaseRequestedIntegrationEvent>
{
    private readonly IStockReservationRepository _reservations;
    private readonly IInventoryItemRepository _items;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderReservationReleaseRequestedIntegrationEventHandler(
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

    public async Task HandleAsync(OrderReservationReleaseRequestedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var active = (await _reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken))
            .Where(r => r.Status == ReservationStatus.Active)
            .ToList();
        if (active.Count == 0) return; // idempotent: nothing left to release

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await ActiveReservationReleaser.ReleaseAsync(active, _items, _movements, @event.OrderId, now, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

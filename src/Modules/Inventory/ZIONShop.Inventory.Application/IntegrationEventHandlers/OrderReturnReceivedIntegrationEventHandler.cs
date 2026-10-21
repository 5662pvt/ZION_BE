using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

/// <summary>
/// Restocks the order's COMMITTED reservations once a return is physically received. Mirrors
/// <see cref="OrderCancelledIntegrationEventHandler"/>'s committed-return path: process ordered by
/// ProductId (same deadlock-avoidance rule), one <see cref="StockMovementType.Return"/> movement per
/// product, one <see cref="IInventoryUnitOfWork.SaveChangesAsync"/>. Reservations are the source of
/// truth for quantities (they include gift lines and reflect what actually left stock); a mismatch
/// against <see cref="OrderReturnReceivedIntegrationEvent.Lines"/> is logged but never blocks the
/// restock. Only <see cref="ReservationStatus.Committed"/> reservations are processed and then marked
/// <see cref="ReservationStatus.Returned"/>, so a duplicate/redelivery of this event is a no-op.
/// </summary>
public class OrderReturnReceivedIntegrationEventHandler : IIntegrationEventHandler<OrderReturnReceivedIntegrationEvent>
{
    private readonly IStockReservationRepository _reservations;
    private readonly IInventoryItemRepository _items;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderReturnReceivedIntegrationEventHandler> _logger;

    public OrderReturnReceivedIntegrationEventHandler(
        IStockReservationRepository reservations,
        IInventoryItemRepository items,
        IStockMovementRepository movements,
        IInventoryUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<OrderReturnReceivedIntegrationEventHandler> logger)
    {
        _reservations = reservations;
        _items = items;
        _movements = movements;
        _uow = uow;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(OrderReturnReceivedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var reservations = await _reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        var toReturn = reservations
            .Where(r => r.Status == ReservationStatus.Committed)
            .OrderBy(r => r.ProductId)
            .ToList();
        if (toReturn.Count == 0) return; // idempotent: nothing left to return (already processed or never committed)

        WarnOnLineMismatch(@event, toReturn);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var reservation in toReturn)
        {
            if (await _items.TryReturnAsync(reservation.ProductId, reservation.Quantity, cancellationToken))
            {
                await _movements.AddAsync(StockMovement.Create(reservation.ProductId, StockMovementType.Return, reservation.Quantity, @event.OrderId.ToString(), now), cancellationToken);
            }

            reservation.MarkReturned();
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }

    private void WarnOnLineMismatch(OrderReturnReceivedIntegrationEvent @event, List<StockReservation> toReturn)
    {
        var reservedByProduct = toReturn
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));
        var linesByProduct = @event.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        if (reservedByProduct.Count != linesByProduct.Count ||
            reservedByProduct.Any(kv => !linesByProduct.TryGetValue(kv.Key, out var qty) || qty != kv.Value))
        {
            _logger.LogWarning(
                "OrderReturnReceivedIntegrationEventHandler: event Lines for order {OrderId} disagree with committed reservations -- using reservations as source of truth",
                @event.OrderId);
        }
    }
}

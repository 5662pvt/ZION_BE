using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

public class OrderConfirmedIntegrationEventHandler : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    private readonly IStockReservationRepository _reservations;
    private readonly IInventoryItemRepository _items;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderConfirmedIntegrationEventHandler> _logger;

    public OrderConfirmedIntegrationEventHandler(
        IStockReservationRepository reservations,
        IInventoryItemRepository items,
        IStockMovementRepository movements,
        IInventoryUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<OrderConfirmedIntegrationEventHandler> logger)
    {
        _reservations = reservations;
        _items = items;
        _movements = movements;
        _uow = uow;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(OrderConfirmedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var active = (await _reservations.GetByOrderIdAsync(@event.OrderId, cancellationToken))
            .Where(r => r.Status == ReservationStatus.Active)
            .ToList();
        if (active.Count == 0) return; // idempotent: nothing active to commit

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var reservation in active)
        {
            var item = await _items.GetByProductIdAsync(reservation.ProductId, cancellationToken);
            if (item is null)
            {
                _logger.LogWarning("Inventory item for product {ProductId} not found while confirming order {OrderId}", reservation.ProductId, @event.OrderId);
                continue;
            }

            var result = item.Commit(reservation.Quantity);
            if (result.IsFailure)
            {
                _logger.LogError("Failed to commit {Quantity} of product {ProductId} for order {OrderId}: {Error}", reservation.Quantity, reservation.ProductId, @event.OrderId, result.Error.Message);
                continue;
            }

            reservation.MarkCommitted();
            await _movements.AddAsync(StockMovement.Create(reservation.ProductId, StockMovementType.Commit, reservation.Quantity, @event.OrderId.ToString(), now), cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

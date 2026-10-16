using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.Services;

/// <summary>
/// Pure business logic for expiring due reservations, kept free of hosting concerns
/// (<see cref="Microsoft.Extensions.Hosting.BackgroundService"/> lives in Infrastructure) so it can
/// be unit tested directly against an in-memory <c>InventoryDbContext</c> with a fake <see cref="TimeProvider"/>.
/// </summary>
public class ReservationExpiryProcessor
{
    private readonly IStockReservationRepository _reservations;
    private readonly IInventoryItemRepository _items;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly IInventoryOutbox _outbox;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ReservationExpiryProcessor> _logger;

    public ReservationExpiryProcessor(
        IStockReservationRepository reservations,
        IInventoryItemRepository items,
        IStockMovementRepository movements,
        IInventoryUnitOfWork uow,
        IInventoryOutbox outbox,
        TimeProvider timeProvider,
        ILogger<ReservationExpiryProcessor> logger)
    {
        _reservations = reservations;
        _items = items;
        _movements = movements;
        _uow = uow;
        _outbox = outbox;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ExpireDueReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expired = await _reservations.GetActiveExpiredAsync(now, cancellationToken: cancellationToken);
        if (expired.Count == 0) return 0;

        // Unlike the integration event handlers, this processor is invoked directly by the background
        // service rather than through the inbox-idempotent dispatch, so no transaction is already open on
        // this scope's DbContext -- open one explicitly so every atomic release below and the final
        // SaveChangesAsync commit (or roll back) together.
        await using var transaction = await _uow.BeginTransactionAsync(cancellationToken);

        // Released across the WHOLE batch ordered by ProductId (not grouped/ordered per order) so a
        // concurrent commit/reserve touching the same products always takes its row locks in the same
        // order as this scan, avoiding a Postgres deadlock (40P01) between them.
        foreach (var reservation in expired.OrderBy(r => r.ProductId))
        {
            if (await _items.TryReleaseAsync(reservation.ProductId, reservation.Quantity, cancellationToken))
            {
                await _movements.AddAsync(StockMovement.Create(reservation.ProductId, StockMovementType.Expire, reservation.Quantity, reservation.OrderId.ToString(), now), cancellationToken);
            }

            reservation.MarkExpired();
        }

        foreach (var orderId in expired.Select(r => r.OrderId).Distinct())
        {
            _outbox.Add(new InventoryReservationExpiredIntegrationEvent(orderId));
        }

        await _uow.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Expired {Count} stock reservation(s) across {OrderCount} order(s)", expired.Count, expired.Select(r => r.OrderId).Distinct().Count());
        return expired.Count;
    }
}

using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

/// <summary>
/// Shared "give back an active reservation" logic used by <see cref="OrderReservationReleaseRequestedIntegrationEventHandler"/>.
/// Releases are applied with the atomic <see cref="IInventoryItemRepository.TryReleaseAsync"/> (no tracked
/// row, no deferred write at <c>SaveChangesAsync</c> time), processing products ordered by ProductId so two
/// concurrent multi-product operations (e.g. this release and a concurrent reserve/commit touching the same
/// products) always take their row locks in the same order and never deadlock.
/// </summary>
internal static class ActiveReservationReleaser
{
    public static async Task ReleaseAsync(
        IReadOnlyList<StockReservation> activeReservations,
        IInventoryItemRepository items,
        IStockMovementRepository movements,
        Guid orderId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var reservation in activeReservations.OrderBy(r => r.ProductId))
        {
            if (await items.TryReleaseAsync(reservation.ProductId, reservation.Quantity, cancellationToken))
            {
                await movements.AddAsync(
                    StockMovement.Create(reservation.ProductId, StockMovementType.Release, reservation.Quantity, orderId.ToString(), now),
                    cancellationToken);
            }

            reservation.MarkReleased();
        }
    }
}

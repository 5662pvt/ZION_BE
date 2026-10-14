using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Application.IntegrationEventHandlers;

/// <summary>
/// Shared "give back an active reservation" logic used by both <see cref="OrderCancelledIntegrationEventHandler"/>
/// (active half of its release) and <see cref="OrderReservationReleaseRequestedIntegrationEventHandler"/>.
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
        foreach (var reservation in activeReservations)
        {
            var item = await items.GetByProductIdAsync(reservation.ProductId, cancellationToken);
            if (item is not null)
            {
                item.Release(reservation.Quantity);
                await movements.AddAsync(
                    StockMovement.Create(reservation.ProductId, StockMovementType.Release, reservation.Quantity, orderId.ToString(), now),
                    cancellationToken);
            }

            reservation.MarkReleased();
        }
    }
}

using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Loyalty.Application.IntegrationEvents;

/// <summary>Defensively releases the points reservation for an order whose inventory reservation failed
/// outright, like Promotions does (today only the expiry/stale path would otherwise catch this).</summary>
public class InventoryReservationFailedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservationFailedIntegrationEvent>
{
    private readonly ILoyaltyPointsService _loyalty;

    public InventoryReservationFailedIntegrationEventHandler(ILoyaltyPointsService loyalty) => _loyalty = loyalty;

    public Task HandleAsync(InventoryReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _loyalty.ReleaseAsync(@event.OrderId, cancellationToken);
}

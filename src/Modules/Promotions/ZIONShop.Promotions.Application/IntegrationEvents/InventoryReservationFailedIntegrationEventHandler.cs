using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Promotions;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Promotions.Application.IntegrationEvents;

/// <summary>Releases the coupon and all gift reservations for an order whose inventory reservation
/// failed outright (today only the 30-min stale processor would otherwise catch this).</summary>
public class InventoryReservationFailedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservationFailedIntegrationEvent>
{
    private readonly IPromotionEvaluator _evaluator;
    private readonly IGiftPromotionEvaluator _giftEvaluator;

    public InventoryReservationFailedIntegrationEventHandler(IPromotionEvaluator evaluator, IGiftPromotionEvaluator giftEvaluator)
    {
        _evaluator = evaluator;
        _giftEvaluator = giftEvaluator;
    }

    public async Task HandleAsync(InventoryReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        await _evaluator.ReleaseAsync(@event.OrderId, cancellationToken);
        await _giftEvaluator.ReleaseGiftsAsync(@event.OrderId, cancellationToken);
    }
}

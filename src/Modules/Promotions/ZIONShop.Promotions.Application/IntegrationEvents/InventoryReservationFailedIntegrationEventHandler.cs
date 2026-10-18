using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Promotions;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Promotions.Application.IntegrationEvents;

/// <summary>Releases the coupon, gift, and flash-sale reservations for an order whose inventory reservation
/// failed outright (today only the 30-min stale processor would otherwise catch this).</summary>
public class InventoryReservationFailedIntegrationEventHandler : IIntegrationEventHandler<InventoryReservationFailedIntegrationEvent>
{
    private readonly IPromotionEvaluator _evaluator;
    private readonly IGiftPromotionEvaluator _giftEvaluator;
    private readonly IFlashSaleEvaluator _flashSaleEvaluator;

    public InventoryReservationFailedIntegrationEventHandler(IPromotionEvaluator evaluator, IGiftPromotionEvaluator giftEvaluator, IFlashSaleEvaluator flashSaleEvaluator)
    {
        _evaluator = evaluator;
        _giftEvaluator = giftEvaluator;
        _flashSaleEvaluator = flashSaleEvaluator;
    }

    public async Task HandleAsync(InventoryReservationFailedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        await _evaluator.ReleaseAsync(@event.OrderId, cancellationToken);
        await _giftEvaluator.ReleaseGiftsAsync(@event.OrderId, cancellationToken);
        await _flashSaleEvaluator.ReleaseAsync(@event.OrderId, cancellationToken);
    }
}

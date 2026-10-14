using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Promotions.Application.IntegrationEvents;

/// <summary>Delegates to <see cref="IPromotionEvaluator.ReleaseAsync"/> and
/// <see cref="IGiftPromotionEvaluator.ReleaseGiftsAsync"/> — same idempotent, xmin-retry release paths
/// used when Orders compensates a failed checkout and by the stale-reservation job.</summary>
public class OrderCancelledIntegrationEventHandler : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    private readonly IPromotionEvaluator _evaluator;
    private readonly IGiftPromotionEvaluator _giftEvaluator;

    public OrderCancelledIntegrationEventHandler(IPromotionEvaluator evaluator, IGiftPromotionEvaluator giftEvaluator)
    {
        _evaluator = evaluator;
        _giftEvaluator = giftEvaluator;
    }

    public async Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        await _evaluator.ReleaseAsync(@event.OrderId, cancellationToken);
        await _giftEvaluator.ReleaseGiftsAsync(@event.OrderId, cancellationToken);
    }
}

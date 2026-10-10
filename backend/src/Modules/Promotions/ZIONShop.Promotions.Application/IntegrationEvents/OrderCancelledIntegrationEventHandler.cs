using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Promotions.Application.IntegrationEvents;

/// <summary>Delegates to <see cref="IPromotionEvaluator.ReleaseAsync"/> — same idempotent, xmin-retry
/// release path used when Orders compensates a failed checkout and by the stale-reservation job.</summary>
public class OrderCancelledIntegrationEventHandler : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    private readonly IPromotionEvaluator _evaluator;

    public OrderCancelledIntegrationEventHandler(IPromotionEvaluator evaluator) => _evaluator = evaluator;

    public Task HandleAsync(OrderCancelledIntegrationEvent @event, CancellationToken cancellationToken = default) =>
        _evaluator.ReleaseAsync(@event.OrderId, cancellationToken);
}

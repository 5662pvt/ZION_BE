using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;

namespace ZIONShop.Promotions.Application.IntegrationEvents;

/// <summary>Moves a Reserved coupon redemption to Committed once its order is confirmed. No counter
/// change: UsageLimit/PerUserLimit already count Reserved+Committed as "used".</summary>
public class OrderConfirmedIntegrationEventHandler : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderConfirmedIntegrationEventHandler(ICouponRedemptionRepository redemptions, IPromotionsUnitOfWork uow, TimeProvider timeProvider)
    {
        _redemptions = redemptions;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(OrderConfirmedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var redemption = await _redemptions.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        if (redemption is null || redemption.Status != RedemptionStatus.Reserved) return; // idempotent no-op

        redemption.MarkCommitted(_timeProvider.GetUtcNow().UtcDateTime);
        _redemptions.Update(redemption);
        await _uow.SaveChangesAsync(cancellationToken);
    }
}

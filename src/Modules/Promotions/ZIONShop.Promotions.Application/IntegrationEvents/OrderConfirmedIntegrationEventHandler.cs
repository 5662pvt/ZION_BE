using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;

namespace ZIONShop.Promotions.Application.IntegrationEvents;

/// <summary>Moves a Reserved coupon redemption and all Reserved gift redemptions of the order to
/// Committed once it is confirmed. No counter change: UsageLimit/PerUserLimit already count
/// Reserved+Committed as "used".</summary>
public class OrderConfirmedIntegrationEventHandler : IIntegrationEventHandler<OrderConfirmedIntegrationEvent>
{
    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IGiftRedemptionRepository _giftRedemptions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public OrderConfirmedIntegrationEventHandler(
        ICouponRedemptionRepository redemptions,
        IGiftRedemptionRepository giftRedemptions,
        IPromotionsUnitOfWork uow,
        TimeProvider timeProvider)
    {
        _redemptions = redemptions;
        _giftRedemptions = giftRedemptions;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(OrderConfirmedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;

        var redemption = await _redemptions.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        if (redemption is not null && redemption.Status == RedemptionStatus.Reserved)
        {
            redemption.MarkCommitted(now);
            _redemptions.Update(redemption);
            changed = true;
        }

        var giftRedemptions = await _giftRedemptions.GetByOrderIdAsync(@event.OrderId, cancellationToken);
        foreach (var gift in giftRedemptions.Where(g => g.Status == RedemptionStatus.Reserved))
        {
            gift.MarkCommitted(now);
            _giftRedemptions.Update(gift);
            changed = true;
        }

        if (changed)
            await _uow.SaveChangesAsync(cancellationToken);
    }
}

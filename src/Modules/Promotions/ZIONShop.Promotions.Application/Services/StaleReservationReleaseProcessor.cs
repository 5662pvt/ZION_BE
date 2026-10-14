using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Domain.Repositories;

namespace ZIONShop.Promotions.Application.Services;

/// <summary>
/// Pure business logic for releasing stale coupon and gift reservations, kept free of hosting concerns
/// (<see cref="Microsoft.Extensions.Hosting.BackgroundService"/> lives in Infrastructure) so it can
/// be unit tested directly with a fake <see cref="TimeProvider"/>.
/// </summary>
public class StaleReservationReleaseProcessor
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IGiftRedemptionRepository _giftRedemptions;
    private readonly IPromotionEvaluator _evaluator;
    private readonly IGiftPromotionEvaluator _giftEvaluator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaleReservationReleaseProcessor> _logger;

    public StaleReservationReleaseProcessor(
        ICouponRedemptionRepository redemptions,
        IGiftRedemptionRepository giftRedemptions,
        IPromotionEvaluator evaluator,
        IGiftPromotionEvaluator giftEvaluator,
        TimeProvider timeProvider,
        ILogger<StaleReservationReleaseProcessor> logger)
    {
        _redemptions = redemptions;
        _giftRedemptions = giftRedemptions;
        _evaluator = evaluator;
        _giftEvaluator = giftEvaluator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ReleaseStaleReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now - StaleAfter;

        var staleCoupons = await _redemptions.GetStaleReservedAsync(cutoff, cancellationToken);
        foreach (var redemption in staleCoupons)
            await _evaluator.ReleaseAsync(redemption.OrderId, cancellationToken);

        var staleGifts = await _giftRedemptions.GetStaleReservedAsync(cutoff, cancellationToken);
        foreach (var orderId in staleGifts.Select(g => g.OrderId).Distinct())
            await _giftEvaluator.ReleaseGiftsAsync(orderId, cancellationToken);

        var total = staleCoupons.Count + staleGifts.Count;
        if (total > 0)
            _logger.LogInformation("Released {CouponCount} stale coupon reservation(s) and {GiftCount} stale gift reservation(s)", staleCoupons.Count, staleGifts.Count);

        return total;
    }
}

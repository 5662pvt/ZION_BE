using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Domain.Repositories;

namespace ZIONShop.Promotions.Application.Services;

/// <summary>
/// Pure business logic for releasing stale coupon reservations, kept free of hosting concerns
/// (<see cref="Microsoft.Extensions.Hosting.BackgroundService"/> lives in Infrastructure) so it can
/// be unit tested directly with a fake <see cref="TimeProvider"/>.
/// </summary>
public class StaleReservationReleaseProcessor
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IPromotionEvaluator _evaluator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StaleReservationReleaseProcessor> _logger;

    public StaleReservationReleaseProcessor(
        ICouponRedemptionRepository redemptions,
        IPromotionEvaluator evaluator,
        TimeProvider timeProvider,
        ILogger<StaleReservationReleaseProcessor> logger)
    {
        _redemptions = redemptions;
        _evaluator = evaluator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ReleaseStaleReservationsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now - StaleAfter;
        var stale = await _redemptions.GetStaleReservedAsync(cutoff, cancellationToken);
        if (stale.Count == 0) return 0;

        foreach (var redemption in stale)
            await _evaluator.ReleaseAsync(redemption.OrderId, cancellationToken);

        _logger.LogInformation("Released {Count} stale coupon reservation(s)", stale.Count);
        return stale.Count;
    }
}

using Microsoft.Extensions.Logging;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Domain.Repositories;

namespace ZIONShop.Loyalty.Application.Services;

/// <summary>Pure business logic for expiring point lots, kept free of hosting concerns
/// (<see cref="Microsoft.Extensions.Hosting.BackgroundService"/> lives in Infrastructure) so it can be unit
/// tested directly with a fake <see cref="TimeProvider"/>.</summary>
public class LoyaltyExpiryProcessor
{
    private const int BatchSize = 500;

    private readonly IPointLotRepository _lots;
    private readonly ILoyaltyMutationService _mutations;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LoyaltyExpiryProcessor> _logger;

    public LoyaltyExpiryProcessor(IPointLotRepository lots, ILoyaltyMutationService mutations, TimeProvider timeProvider, ILogger<LoyaltyExpiryProcessor> logger)
    {
        _lots = lots;
        _mutations = mutations;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<int> ExpireLotsAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expired = await _lots.GetExpiredBatchAsync(now, BatchSize, cancellationToken);

        var totalPoints = 0;
        foreach (var lot in expired)
            totalPoints += await _mutations.ExpireLotAsync(lot.Id, cancellationToken);

        if (expired.Count > 0)
            _logger.LogInformation("Expired {LotCount} point lot(s), removing {PointCount} point(s)", expired.Count, totalPoints);

        return expired.Count;
    }
}

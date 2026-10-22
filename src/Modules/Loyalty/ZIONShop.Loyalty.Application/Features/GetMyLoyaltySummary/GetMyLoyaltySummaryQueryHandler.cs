using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.GetMyLoyaltySummary;

public class GetMyLoyaltySummaryQueryHandler : IRequestHandler<GetMyLoyaltySummaryQuery, Result<LoyaltySummaryDto>>
{
    private static readonly TimeSpan ExpiringSoonWindow = TimeSpan.FromDays(30);

    private readonly IPointAccountRepository _accounts;
    private readonly IPointLotRepository _lots;
    private readonly IOptions<LoyaltyOptions> _options;
    private readonly TimeProvider _timeProvider;

    public GetMyLoyaltySummaryQueryHandler(IPointAccountRepository accounts, IPointLotRepository lots, IOptions<LoyaltyOptions> options, TimeProvider timeProvider)
    {
        _accounts = accounts;
        _lots = lots;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoyaltySummaryDto>> Handle(GetMyLoyaltySummaryQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var account = await _accounts.GetByUserIdAsync(request.UserId, cancellationToken);
        var expiringSoon = await _lots.GetExpiringSoonAsync(request.UserId, now, now + ExpiringSoonWindow, cancellationToken);
        var expiredUnprocessed = await _lots.GetExpiredRemainingSumAsync(request.UserId, now, cancellationToken);
        var effectiveBalance = Math.Max(0, (account?.Balance ?? 0) - expiredUnprocessed);

        var options = _options.Value;
        var dto = new LoyaltySummaryDto(
            effectiveBalance,
            expiringSoon.Sum(l => l.Remaining),
            expiringSoon.Count > 0 ? expiringSoon.Min(l => l.ExpiresAt) : null,
            options.MaxRedeemPercent,
            options.EarnRatePercent,
            options.ReviewBonus);

        return Result.Success(dto);
    }
}

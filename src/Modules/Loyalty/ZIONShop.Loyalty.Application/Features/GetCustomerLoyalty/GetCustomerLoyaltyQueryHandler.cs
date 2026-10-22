using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.Loyalty.Application.Mappings;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.GetCustomerLoyalty;

public class GetCustomerLoyaltyQueryHandler : IRequestHandler<GetCustomerLoyaltyQuery, Result<CustomerLoyaltyDto>>
{
    private const int FirstPageSize = 20;
    private static readonly TimeSpan ExpiringSoonWindow = TimeSpan.FromDays(30);

    private readonly IPointAccountRepository _accounts;
    private readonly IPointLotRepository _lots;
    private readonly IPointTransactionRepository _transactions;
    private readonly IOptions<LoyaltyOptions> _options;
    private readonly TimeProvider _timeProvider;

    public GetCustomerLoyaltyQueryHandler(
        IPointAccountRepository accounts,
        IPointLotRepository lots,
        IPointTransactionRepository transactions,
        IOptions<LoyaltyOptions> options,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _lots = lots;
        _transactions = transactions;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CustomerLoyaltyDto>> Handle(GetCustomerLoyaltyQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var account = await _accounts.GetByUserIdAsync(request.UserId, cancellationToken);
        var expiringSoon = await _lots.GetExpiringSoonAsync(request.UserId, now, now + ExpiringSoonWindow, cancellationToken);
        var activeLots = await _lots.GetActiveForUserAsync(request.UserId, now, cancellationToken);
        var expiredUnprocessed = await _lots.GetExpiredRemainingSumAsync(request.UserId, now, cancellationToken);
        var effectiveBalance = Math.Max(0, (account?.Balance ?? 0) - expiredUnprocessed);

        var recentTransactions = _transactions.Query()
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(FirstPageSize)
            .Select(t => t.ToDto())
            .ToList();

        var options = _options.Value;
        var summary = new LoyaltySummaryDto(
            effectiveBalance,
            expiringSoon.Sum(l => l.Remaining),
            expiringSoon.Count > 0 ? expiringSoon.Min(l => l.ExpiresAt) : null,
            options.MaxRedeemPercent,
            options.EarnRatePercent,
            options.ReviewBonus);

        var dto = new CustomerLoyaltyDto(summary, recentTransactions, activeLots.Select(l => l.ToDto()).ToList());
        return Result.Success(dto);
    }
}

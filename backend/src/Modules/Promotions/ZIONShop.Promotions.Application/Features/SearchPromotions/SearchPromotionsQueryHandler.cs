using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.SearchPromotions;

public class SearchPromotionsQueryHandler : IRequestHandler<SearchPromotionsQuery, Result<PagedResult<PromotionSummaryDto>>>
{
    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly TimeProvider _timeProvider;

    public SearchPromotionsQueryHandler(IPromotionRepository promotions, ICouponCodeRepository coupons, TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PagedResult<PromotionSummaryDto>>> Handle(SearchPromotionsQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        IQueryable<Promotion> query = _promotions.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<PromotionStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = status switch
            {
                PromotionStatus.Inactive => query.Where(p => !p.IsActive),
                PromotionStatus.Scheduled => query.Where(p => p.IsActive && now < p.StartsAt),
                PromotionStatus.Expired => query.Where(p => p.IsActive && now > p.EndsAt),
                PromotionStatus.Active => query.Where(p => p.IsActive && now >= p.StartsAt && now <= p.EndsAt),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = EscapeLikePattern(request.Keyword.Trim()).ToUpperInvariant();
            query = query.Where(p => EF.Functions.Like(p.Name.ToUpper(), $"%{kw}%"));
        }

        query = query.OrderByDescending(p => p.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var promotions = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var ids = promotions.Select(p => p.Id).ToList();
        var counts = await _coupons.Query().AsNoTracking()
            .Where(c => ids.Contains(c.PromotionId))
            .GroupBy(c => c.PromotionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var items = promotions
            .Select(p => p.ToSummaryDto(counts.TryGetValue(p.Id, out var count) ? count : 0, now))
            .ToList();

        return Result.Success(PagedResult<PromotionSummaryDto>.Create(items, page, pageSize, total));
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

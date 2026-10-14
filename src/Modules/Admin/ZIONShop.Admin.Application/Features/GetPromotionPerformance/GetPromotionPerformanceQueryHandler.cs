using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetPromotionPerformance;

public class GetPromotionPerformanceQueryHandler : IRequestHandler<GetPromotionPerformanceQuery, Result<IReadOnlyList<PromotionPerformanceDto>>>
{
    private readonly IOrderReportReader _orders;
    private readonly IPromotionLookup _promotions;
    private readonly TimeProvider _timeProvider;

    public GetPromotionPerformanceQueryHandler(IOrderReportReader orders, IPromotionLookup promotions, TimeProvider timeProvider)
    {
        _orders = orders;
        _promotions = promotions;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<PromotionPerformanceDto>>> Handle(GetPromotionPerformanceQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<IReadOnlyList<PromotionPerformanceDto>>(rangeResult.Error);
        var range = rangeResult.Value;

        var orders = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);

        var results = new List<PromotionPerformanceDto>();

        var couponGroups = orders
            .Where(o => !string.IsNullOrWhiteSpace(o.CouponCode))
            .GroupBy(o => o.CouponCode!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (couponGroups.Count > 0)
        {
            var codes = couponGroups.Select(g => g.Key).ToList();
            var resolved = await _promotions.ResolveCouponCodesAsync(codes, cancellationToken);

            foreach (var group in couponGroups)
            {
                var orderCount = group.Count();
                var discountTotal = group.Sum(o => o.DiscountAmount);

                if (resolved.TryGetValue(group.Key, out var promotion))
                {
                    results.Add(new PromotionPerformanceDto(promotion.PromotionId, promotion.Name, promotion.Type, orderCount, discountTotal, 0));
                }
                else
                {
                    results.Add(new PromotionPerformanceDto(Guid.Empty, group.Key, "Unknown", orderCount, discountTotal, 0));
                }
            }
        }

        var giftGroups = orders
            .SelectMany(o => o.Lines.Where(l => l.IsGift && l.GiftPromotionId.HasValue).Select(l => (OrderId: o.OrderId, Line: l)))
            .GroupBy(x => x.Line.GiftPromotionId!.Value)
            .ToList();

        if (giftGroups.Count > 0)
        {
            var ids = giftGroups.Select(g => g.Key).ToList();
            var resolved = await _promotions.GetByIdsAsync(ids, cancellationToken);

            foreach (var group in giftGroups)
            {
                var orderCount = group.Select(x => x.OrderId).Distinct().Count();
                var giftQuantity = group.Sum(x => x.Line.Quantity);

                if (resolved.TryGetValue(group.Key, out var promotion))
                {
                    results.Add(new PromotionPerformanceDto(promotion.PromotionId, promotion.Name, promotion.Type, orderCount, 0, giftQuantity));
                }
                else
                {
                    results.Add(new PromotionPerformanceDto(group.Key, group.Key.ToString(), "Gift", orderCount, 0, giftQuantity));
                }
            }
        }

        var sorted = results.OrderByDescending(r => r.Orders).ToList();
        return Result.Success<IReadOnlyList<PromotionPerformanceDto>>(sorted);
    }
}

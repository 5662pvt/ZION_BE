using System.Globalization;
using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Promotions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ExportPromotionsCsv;

public class ExportPromotionsCsvQueryHandler : IRequestHandler<ExportPromotionsCsvQuery, Result<CsvFile>>
{
    private readonly IOrderReportReader _orders;
    private readonly IPromotionLookup _promotions;
    private readonly TimeProvider _timeProvider;

    public ExportPromotionsCsvQueryHandler(IOrderReportReader orders, IPromotionLookup promotions, TimeProvider timeProvider)
    {
        _orders = orders;
        _promotions = promotions;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CsvFile>> Handle(ExportPromotionsCsvQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<CsvFile>(rangeResult.Error);
        var range = rangeResult.Value;

        var orders = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);
        var rows = new List<IReadOnlyList<string>>();

        var couponGroups = orders
            .Where(o => !string.IsNullOrWhiteSpace(o.CouponCode))
            .GroupBy(o => o.CouponCode!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (couponGroups.Count > 0)
        {
            var resolved = await _promotions.ResolveCouponCodesAsync(couponGroups.Select(g => g.Key).ToList(), cancellationToken);
            foreach (var group in couponGroups)
            {
                var hasPromotion = resolved.TryGetValue(group.Key, out var promo);
                var name = hasPromotion ? promo!.Name : group.Key;
                var type = ToVietnameseTypeLabel(hasPromotion ? promo!.Type : "Unknown");
                rows.Add(new[]
                {
                    name,
                    type,
                    group.Count().ToString(CultureInfo.InvariantCulture),
                    CsvWriter.FormatMoney(group.Sum(o => o.DiscountAmount)),
                    "0"
                });
            }
        }

        var giftGroups = orders
            .SelectMany(o => o.Lines.Where(l => l.IsGift && l.GiftPromotionId.HasValue).Select(l => (OrderId: o.OrderId, Line: l)))
            .GroupBy(x => x.Line.GiftPromotionId!.Value)
            .ToList();

        if (giftGroups.Count > 0)
        {
            var resolved = await _promotions.GetByIdsAsync(giftGroups.Select(g => g.Key).ToList(), cancellationToken);
            foreach (var group in giftGroups)
            {
                var hasPromotion = resolved.TryGetValue(group.Key, out var promo);
                var name = hasPromotion ? promo!.Name : group.Key.ToString();
                var type = ToVietnameseTypeLabel(hasPromotion ? promo!.Type : "Gift");
                var orderCount = group.Select(x => x.OrderId).Distinct().Count();
                rows.Add(new[]
                {
                    name,
                    type,
                    orderCount.ToString(CultureInfo.InvariantCulture),
                    "0",
                    group.Sum(x => x.Line.Quantity).ToString(CultureInfo.InvariantCulture)
                });
            }
        }

        var content = CsvWriter.Write(new[] { "Chương trình", "Loại", "Số đơn", "Tổng giảm", "SL quà" }, rows);
        var fileName = $"promotions_{range.From:yyyyMMdd}_{range.To:yyyyMMdd}.csv";

        return Result.Success(new CsvFile(fileName, content));
    }

    private static string ToVietnameseTypeLabel(string type) => type switch
    {
        "PercentageOff" => "Giảm theo %",
        "FixedAmountOff" => "Giảm số tiền",
        "FreeShipping" => "Miễn phí vận chuyển",
        "Gift" => "Quà tặng",
        _ => "Không xác định"
    };
}

using System.Globalization;
using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Contracts.Orders;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ExportRevenueCsv;

public class ExportRevenueCsvQueryHandler : IRequestHandler<ExportRevenueCsvQuery, Result<CsvFile>>
{
    private readonly IOrderReportReader _orders;
    private readonly TimeProvider _timeProvider;

    public ExportRevenueCsvQueryHandler(IOrderReportReader orders, TimeProvider timeProvider)
    {
        _orders = orders;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CsvFile>> Handle(ExportRevenueCsvQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<CsvFile>(rangeResult.Error);
        var range = rangeResult.Value;

        var orders = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);

        decimal OrderTotal(CompletedOrderRow o) => o.Subtotal - o.DiscountAmount - o.PointsDiscount + o.ShippingFee;

        var rows = range.Buckets.Select(b =>
        {
            var inBucket = orders.Where(o => o.CompletedAtUtc >= b.StartUtc && o.CompletedAtUtc < b.EndUtcExclusive).ToList();
            return (IReadOnlyList<string>)new[]
            {
                b.Label,
                inBucket.Count.ToString(CultureInfo.InvariantCulture),
                CsvWriter.FormatMoney(inBucket.Sum(OrderTotal))
            };
        });

        var content = CsvWriter.Write(new[] { "Kỳ", "Số đơn hoàn tất", "Doanh thu" }, rows);
        var fileName = $"revenue_{range.From:yyyyMMdd}_{range.To:yyyyMMdd}.csv";

        return Result.Success(new CsvFile(fileName, content));
    }
}

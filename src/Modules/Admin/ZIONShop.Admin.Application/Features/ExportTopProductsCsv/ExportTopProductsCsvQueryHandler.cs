using System.Globalization;
using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Contracts.Orders;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ExportTopProductsCsv;

public class ExportTopProductsCsvQueryHandler : IRequestHandler<ExportTopProductsCsvQuery, Result<CsvFile>>
{
    private readonly IOrderReportReader _orders;
    private readonly TimeProvider _timeProvider;

    public ExportTopProductsCsvQueryHandler(IOrderReportReader orders, TimeProvider timeProvider)
    {
        _orders = orders;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CsvFile>> Handle(ExportTopProductsCsvQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<CsvFile>(rangeResult.Error);
        var range = rangeResult.Value;

        var orders = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);

        var grouped = orders
            .SelectMany(o => o.Lines.Where(l => !l.IsGift).Select(l => (OrderId: o.OrderId, Line: l)))
            .GroupBy(x => x.Line.ProductId)
            .Select(g => new
            {
                Name = g.First().Line.ProductName,
                Quantity = g.Sum(x => x.Line.Quantity),
                Orders = g.Select(x => x.OrderId).Distinct().Count(),
                Revenue = g.Sum(x => x.Line.UnitPrice * x.Line.Quantity)
            })
            .OrderByDescending(p => p.Revenue)
            .ToList();

        var rows = grouped.Select(p => (IReadOnlyList<string>)new[]
        {
            p.Name,
            p.Quantity.ToString(CultureInfo.InvariantCulture),
            p.Orders.ToString(CultureInfo.InvariantCulture),
            CsvWriter.FormatMoney(p.Revenue)
        });

        var content = CsvWriter.Write(new[] { "Sản phẩm", "Số lượng", "Số đơn", "Doanh thu trước giảm giá" }, rows);
        var fileName = $"top-products_{range.From:yyyyMMdd}_{range.To:yyyyMMdd}.csv";

        return Result.Success(new CsvFile(fileName, content));
    }
}

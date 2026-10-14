using MediatR;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Contracts.Orders;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetTopProducts;

public class GetTopProductsQueryHandler : IRequestHandler<GetTopProductsQuery, Result<IReadOnlyList<TopProductDto>>>
{
    private readonly IOrderReportReader _orders;
    private readonly TimeProvider _timeProvider;

    public GetTopProductsQueryHandler(IOrderReportReader orders, TimeProvider timeProvider)
    {
        _orders = orders;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<TopProductDto>>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<IReadOnlyList<TopProductDto>>(rangeResult.Error);
        var range = rangeResult.Value;

        var orders = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);

        var grouped = orders
            .SelectMany(o => o.Lines.Where(l => !l.IsGift).Select(l => (OrderId: o.OrderId, Line: l)))
            .GroupBy(x => x.Line.ProductId)
            .Select(g => new TopProductDto(
                g.Key,
                g.First().Line.ProductName,
                g.Sum(x => x.Line.Quantity),
                g.Sum(x => x.Line.UnitPrice * x.Line.Quantity),
                g.Select(x => x.OrderId).Distinct().Count()))
            .ToList();

        var sortBy = request.SortBy ?? "revenue";
        IEnumerable<TopProductDto> sorted = sortBy.Equals("quantity", StringComparison.OrdinalIgnoreCase)
            ? grouped.OrderByDescending(p => p.Quantity)
            : grouped.OrderByDescending(p => p.Revenue);

        IReadOnlyList<TopProductDto> top = sorted.Take(request.Limit).ToList();
        return Result.Success(top);
    }
}

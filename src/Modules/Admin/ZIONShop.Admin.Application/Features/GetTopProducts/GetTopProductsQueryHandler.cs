using MediatR;
using ZIONShop.Admin.Application.Caching;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Caching;
using ZIONShop.Contracts.Orders;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetTopProducts;

public class GetTopProductsQueryHandler : IRequestHandler<GetTopProductsQuery, Result<IReadOnlyList<TopProductDto>>>
{
    private readonly IOrderReportReader _orders;
    private readonly TimeProvider _timeProvider;
    private readonly IDistributedCacheService _cache;

    public GetTopProductsQueryHandler(IOrderReportReader orders, TimeProvider timeProvider, IDistributedCacheService cache)
    {
        _orders = orders;
        _timeProvider = timeProvider;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<TopProductDto>>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<IReadOnlyList<TopProductDto>>(rangeResult.Error);
        var range = rangeResult.Value;

        var top = await _cache.GetOrCreateAsync(
            ReportCacheKeys.TopProducts(range.From, range.To, request.Limit, request.SortBy),
            async ct => (IReadOnlyList<TopProductDto>?)await BuildAsync(range, request, ct),
            TimeSpan.FromSeconds(60),
            cancellationToken);

        return Result.Success(top ?? Array.Empty<TopProductDto>());
    }

    private async Task<IReadOnlyList<TopProductDto>> BuildAsync(ReportRange range, GetTopProductsQuery request, CancellationToken cancellationToken)
    {
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

        return sorted.Take(request.Limit).ToList();
    }
}

using MediatR;
using ZIONShop.Admin.Application.Caching;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Caching;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.GetTopCategories;

public class GetTopCategoriesQueryHandler : IRequestHandler<GetTopCategoriesQuery, Result<IReadOnlyList<TopCategoryDto>>>
{
    private const string UncategorizedName = "Chưa phân loại";

    private readonly IOrderReportReader _orders;
    private readonly IProductCatalogReader _catalog;
    private readonly ICategoryLookup _categories;
    private readonly TimeProvider _timeProvider;
    private readonly IDistributedCacheService _cache;

    public GetTopCategoriesQueryHandler(
        IOrderReportReader orders,
        IProductCatalogReader catalog,
        ICategoryLookup categories,
        TimeProvider timeProvider,
        IDistributedCacheService cache)
    {
        _orders = orders;
        _catalog = catalog;
        _categories = categories;
        _timeProvider = timeProvider;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<TopCategoryDto>>> Handle(GetTopCategoriesQuery request, CancellationToken cancellationToken)
    {
        var rangeResult = ReportRange.Create(request.From, request.To, _timeProvider);
        if (rangeResult.IsFailure) return Result.Failure<IReadOnlyList<TopCategoryDto>>(rangeResult.Error);
        var range = rangeResult.Value;

        var top = await _cache.GetOrCreateAsync(
            ReportCacheKeys.TopCategories(range.From, range.To, request.SortBy),
            async ct => (IReadOnlyList<TopCategoryDto>?)await BuildAsync(range, request, ct),
            TimeSpan.FromSeconds(60),
            cancellationToken);

        return Result.Success(top ?? Array.Empty<TopCategoryDto>());
    }

    private async Task<IReadOnlyList<TopCategoryDto>> BuildAsync(ReportRange range, GetTopCategoriesQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orders.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, cancellationToken);
        var products = await _catalog.ListAsync(cancellationToken);
        var categories = await _categories.ListAsync(cancellationToken);

        var productCategoryMap = products.ToDictionary(p => p.ProductId, p => p.CategoryId);
        var categoryNameMap = categories.ToDictionary(c => c.CategoryId, c => c.Name);

        var grouped = orders
            .SelectMany(o => o.Lines.Where(l => !l.IsGift))
            .GroupBy(l => productCategoryMap.TryGetValue(l.ProductId, out var categoryId) ? categoryId : null)
            .Select(g => new TopCategoryDto(
                g.Key,
                g.Key.HasValue && categoryNameMap.TryGetValue(g.Key.Value, out var name) ? name : UncategorizedName,
                g.Sum(l => l.Quantity),
                g.Sum(l => l.UnitPrice * l.Quantity)))
            .ToList();

        var sortBy = request.SortBy ?? "revenue";
        IEnumerable<TopCategoryDto> sorted = sortBy.Equals("quantity", StringComparison.OrdinalIgnoreCase)
            ? grouped.OrderByDescending(c => c.Quantity)
            : grouped.OrderByDescending(c => c.Revenue);

        return sorted.ToList();
    }
}

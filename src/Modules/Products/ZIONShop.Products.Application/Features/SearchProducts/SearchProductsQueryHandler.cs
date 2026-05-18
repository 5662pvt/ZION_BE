using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.SearchProducts;

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, Result<PagedResult<ProductSummaryDto>>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;

    public SearchProductsQueryHandler(IProductRepository products, ICategoryRepository categories)
    {
        _products = products;
        _categories = categories;
    }

    public async Task<Result<PagedResult<ProductSummaryDto>>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Product> query = _products.Query()
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = request.Keyword.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{kw}%") || EF.Functions.Like(p.Sku.Value, $"%{kw}%"));
        }

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == request.CategoryId);

        if (request.MinPrice.HasValue)
            query = query.Where(p => p.Price.Amount >= request.MinPrice.Value);

        if (request.MaxPrice.HasValue)
            query = query.Where(p => p.Price.Amount <= request.MaxPrice.Value);

        query = (request.Sort ?? "name") switch
        {
            "price_asc" => query.OrderBy(p => p.Price.Amount),
            "price_desc" => query.OrderByDescending(p => p.Price.Amount),
            "newest" => query.OrderByDescending(p => p.CreatedDate),
            _ => query.OrderBy(p => p.Name)
        };

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                Sku = p.Sku.Value,
                Price = p.Price.Amount,
                Currency = p.Price.Currency,
                Status = p.Status,
                p.CategoryId,
                PrimaryImageUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var categoryIds = items.Where(i => i.CategoryId.HasValue).Select(i => i.CategoryId!.Value).Distinct().ToList();
        var categoryNames = await _categories.Query()
            .AsNoTracking()
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var dtos = items.Select(i => new ProductSummaryDto(
            i.Id, i.Name, i.Slug, i.Sku, i.Price, i.Currency, i.Status.ToString(),
            i.CategoryId.HasValue && categoryNames.TryGetValue(i.CategoryId.Value, out var cn) ? cn : null,
            i.PrimaryImageUrl)).ToList();

        return Result.Success(PagedResult<ProductSummaryDto>.Create(dtos, page, pageSize, total));
    }
}

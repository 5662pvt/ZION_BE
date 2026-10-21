using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.SuggestProducts;

public class SuggestProductsQueryHandler : IRequestHandler<SuggestProductsQuery, Result<IReadOnlyList<ProductSuggestionDto>>>
{
    private readonly IProductRepository _products;

    public SuggestProductsQueryHandler(IProductRepository products) => _products = products;

    // q's 2..100 char rule is enforced here rather than via FluentValidation: ValidationBehavior always
    // prefixes a FluentValidation failure code with the property name ("Q.<code>"), which cannot produce
    // the contract-exact "Products.InvalidSuggestQuery" code (same reasoning as PlaceOrderCommandValidator).
    public async Task<Result<IReadOnlyList<ProductSuggestionDto>>> Handle(SuggestProductsQuery request, CancellationToken cancellationToken)
    {
        var q = request.Q?.Trim() ?? string.Empty;
        if (q.Length is < 2 or > 100)
            return Result.Failure<IReadOnlyList<ProductSuggestionDto>>(ProductsErrors.InvalidSuggestQuery);

        var limit = request.Limit is >= 1 and <= 10 ? request.Limit.Value : 8;
        var kw = q.ToUpperInvariant();

        var items = await _products.Query()
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published)
            .Where(p =>
                EF.Functions.Like(p.Name.ToUpper(), $"%{kw}%") ||
                p.Variants.Any(v => v.IsActive && EF.Functions.Like(v.Sku.Value.ToUpper(), $"%{kw}%")) ||
                (p.Brand != null && EF.Functions.Like(p.Brand.ToUpper(), $"%{kw}%")))
            .OrderByDescending(p => EF.Functions.Like(p.Name.ToUpper(), $"{kw}%"))
            .ThenBy(p => p.Name)
            .Take(limit)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Slug,
                Sku = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Price.Amount).ThenBy(v => v.DisplayOrder).Select(v => v.Sku.Value).FirstOrDefault(),
                Currency = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Price.Amount).ThenBy(v => v.DisplayOrder).Select(v => v.Price.Currency).FirstOrDefault(),
                p.MinPrice,
                p.MaxPrice,
                ThumbnailUrl = p.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                VariantCount = p.Variants.Count(v => v.IsActive)
            })
            .ToListAsync(cancellationToken);

        var dtos = items
            .Select(i => new ProductSuggestionDto(i.Id, i.Name, i.Slug, i.Sku ?? string.Empty, i.MinPrice, i.Currency ?? "VND", i.ThumbnailUrl, i.MinPrice, i.MaxPrice, i.VariantCount))
            .ToList();

        return Result.Success<IReadOnlyList<ProductSuggestionDto>>(dtos);
    }
}

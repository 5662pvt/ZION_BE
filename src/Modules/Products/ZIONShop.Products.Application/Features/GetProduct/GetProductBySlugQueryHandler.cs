using MediatR;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.GetProduct;

public class GetProductBySlugQueryHandler : IRequestHandler<GetProductBySlugQuery, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IDistributedCacheService _cache;

    public GetProductBySlugQueryHandler(IProductRepository products, ICategoryRepository categories, IDistributedCacheService cache)
    {
        _products = products;
        _categories = categories;
        _cache = cache;
    }

    public async Task<Result<ProductDto>> Handle(GetProductBySlugQuery request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        var cached = await _cache.GetOrCreateAsync(
            ProductCacheKeys.DetailBySlug(slug),
            ct => LoadPublishedAsync(slug, ct),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        return cached is null ? Result.Failure<ProductDto>(ProductsErrors.ProductNotFound) : Result.Success(cached);
    }

    private async Task<ProductDto?> LoadPublishedAsync(string slug, CancellationToken cancellationToken)
    {
        var product = await _products.GetBySlugAsync(slug, cancellationToken);
        if (product is null || product.Status != ProductStatus.Published)
            return null;

        string? categoryName = null;
        if (product.CategoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(product.CategoryId.Value, cancellationToken);
            categoryName = category?.Name;
        }
        return product.ToDto(categoryName);
    }
}

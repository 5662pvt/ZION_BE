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

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IDistributedCacheService _cache;

    public GetProductByIdQueryHandler(IProductRepository products, ICategoryRepository categories, IDistributedCacheService cache)
    {
        _products = products;
        _categories = categories;
        _cache = cache;
    }

    public async Task<Result<ProductDto>> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var cached = await _cache.GetOrCreateAsync(
            ProductCacheKeys.DetailById(request.Id),
            ct => LoadPublishedAsync(request.Id, ct),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        return cached is null ? Result.Failure<ProductDto>(ProductsErrors.ProductNotFound) : Result.Success(cached);
    }

    private async Task<ProductDto?> LoadPublishedAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(id, cancellationToken);
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

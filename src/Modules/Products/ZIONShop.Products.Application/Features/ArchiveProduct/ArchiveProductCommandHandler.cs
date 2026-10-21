using MediatR;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.ArchiveProduct;

public class ArchiveProductCommandHandler : IRequestHandler<ArchiveProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;
    private readonly IDistributedCacheService _cache;

    public ArchiveProductCommandHandler(IProductRepository products, ICategoryRepository categories, IProductsUnitOfWork uow, IDistributedCacheService cache)
    {
        _products = products;
        _categories = categories;
        _uow = uow;
        _cache = cache;
    }

    public async Task<Result<ProductDto>> Handle(ArchiveProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null) return Result.Failure<ProductDto>(ProductsErrors.ProductNotFound);

        product.Archive();
        _products.Update(product);
        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.RemoveManyAsync(ProductCacheKeys.DetailById(product.Id), ProductCacheKeys.DetailBySlug(product.Slug));

        string? categoryName = null;
        if (product.CategoryId.HasValue)
        {
            var category = await _categories.GetByIdAsync(product.CategoryId.Value, cancellationToken);
            categoryName = category?.Name;
        }

        return Result.Success(product.ToDto(categoryName, includeInactiveVariants: true));
    }
}

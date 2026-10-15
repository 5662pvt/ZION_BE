using MediatR;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Mappings;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.UpdateProduct;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result<ProductDto>>
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;
    private readonly IDistributedCacheService _cache;

    public UpdateProductCommandHandler(IProductRepository products, ICategoryRepository categories, IProductsUnitOfWork uow, IDistributedCacheService cache)
    {
        _products = products;
        _categories = categories;
        _uow = uow;
        _cache = cache;
    }

    public async Task<Result<ProductDto>> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _products.GetByIdAsync(request.Id, cancellationToken);
        if (product is null) return Result.Failure<ProductDto>(ProductsErrors.ProductNotFound);

        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await _categories.GetByIdAsync(request.CategoryId.Value, cancellationToken);
            if (category is null) return Result.Failure<ProductDto>(ProductsErrors.CategoryNotFound);
        }

        product.Update(request.Name, request.Description, Money.Create(request.Price, request.Currency), request.CategoryId, request.Brand);
        _products.Update(product);
        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.RemoveManyAsync(ProductCacheKeys.DetailById(product.Id), ProductCacheKeys.DetailBySlug(product.Slug));

        return Result.Success(product.ToDto(category?.Name));
    }
}

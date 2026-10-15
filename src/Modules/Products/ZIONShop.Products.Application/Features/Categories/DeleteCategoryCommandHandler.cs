using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly ICategoryRepository _categories;
    private readonly IProductRepository _products;
    private readonly IProductsUnitOfWork _uow;
    private readonly IDistributedCacheService _cache;

    public DeleteCategoryCommandHandler(ICategoryRepository categories, IProductRepository products, IProductsUnitOfWork uow, IDistributedCacheService cache)
    {
        _categories = categories;
        _products = products;
        _uow = uow;
        _cache = cache;
    }

    public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.Id, cancellationToken);
        if (category is null) return Result.Failure(ProductsErrors.CategoryNotFound);

        var hasChildren = await _categories.Query()
            .AnyAsync(c => c.ParentId == request.Id, cancellationToken);
        if (hasChildren) return Result.Failure(ProductsErrors.CategoryHasChildren);

        var hasProducts = await _products.Query()
            .AnyAsync(p => p.CategoryId == request.Id, cancellationToken);
        if (hasProducts) return Result.Failure(ProductsErrors.CategoryHasProducts);

        _categories.Remove(category);
        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(ProductCacheKeys.CategoryTree, cancellationToken);

        return Result.Success();
    }
}

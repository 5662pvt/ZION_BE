using MediatR;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public class UpdateCategoryCommandHandler : IRequestHandler<UpdateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;
    private readonly IDistributedCacheService _cache;

    public UpdateCategoryCommandHandler(ICategoryRepository categories, IProductsUnitOfWork uow, IDistributedCacheService cache)
    {
        _categories = categories;
        _uow = uow;
        _cache = cache;
    }

    public async Task<Result<CategoryDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _categories.GetByIdAsync(request.Id, cancellationToken);
        if (category is null) return Result.Failure<CategoryDto>(ProductsErrors.CategoryNotFound);

        category.Update(request.Name, request.Description, request.DisplayOrder);
        _categories.Update(category);
        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(ProductCacheKeys.CategoryTree, cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.Slug, category.Description, category.ParentId, category.DisplayOrder));
    }
}

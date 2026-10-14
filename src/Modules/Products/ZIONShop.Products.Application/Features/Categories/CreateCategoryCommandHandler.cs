using MediatR;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, Result<CategoryDto>>
{
    private readonly ICategoryRepository _categories;
    private readonly IProductsUnitOfWork _uow;

    public CreateCategoryCommandHandler(ICategoryRepository categories, IProductsUnitOfWork uow)
    {
        _categories = categories;
        _uow = uow;
    }

    public async Task<Result<CategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await _categories.SlugExistsAsync(slug, cancellationToken))
            return Result.Failure<CategoryDto>(ProductsErrors.CategorySlugAlreadyExists);

        if (request.ParentId.HasValue)
        {
            var parent = await _categories.GetByIdAsync(request.ParentId.Value, cancellationToken);
            if (parent is null) return Result.Failure<CategoryDto>(ProductsErrors.CategoryNotFound);
        }

        var category = Category.Create(request.Name, slug, request.ParentId, request.Description, request.DisplayOrder);
        await _categories.AddAsync(category, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new CategoryDto(category.Id, category.Name, category.Slug, category.Description, category.ParentId, category.DisplayOrder));
    }
}

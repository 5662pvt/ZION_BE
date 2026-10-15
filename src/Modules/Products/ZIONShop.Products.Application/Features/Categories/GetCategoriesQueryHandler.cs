using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Caching;
using ZIONShop.Products.Application.Caching;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryTreeNodeDto>>>
{
    private readonly ICategoryRepository _categories;
    private readonly IDistributedCacheService _cache;

    public GetCategoriesQueryHandler(ICategoryRepository categories, IDistributedCacheService cache)
    {
        _categories = categories;
        _cache = cache;
    }

    public async Task<Result<IReadOnlyList<CategoryTreeNodeDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        if (!request.AsTree)
        {
            var flat = await LoadAsync(asTree: false, cancellationToken);
            return Result.Success(flat);
        }

        var tree = await _cache.GetOrCreateAsync(
            ProductCacheKeys.CategoryTree,
            async ct => (IReadOnlyList<CategoryTreeNodeDto>?)await LoadAsync(asTree: true, ct),
            TimeSpan.FromMinutes(30),
            cancellationToken);

        return Result.Success(tree ?? Array.Empty<CategoryTreeNodeDto>());
    }

    private async Task<IReadOnlyList<CategoryTreeNodeDto>> LoadAsync(bool asTree, CancellationToken cancellationToken)
    {
        var all = await _categories.Query().AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        return asTree ? BuildTree(all, null) : all.Select(MapFlat).ToList();
    }

    private static IReadOnlyList<CategoryTreeNodeDto> BuildTree(List<Category> all, Guid? parentId) =>
        all.Where(c => c.ParentId == parentId)
            .Select(c => new CategoryTreeNodeDto(c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder, BuildTree(all, c.Id)))
            .ToList();

    private static CategoryTreeNodeDto MapFlat(Category c) =>
        new(c.Id, c.Name, c.Slug, c.Description, c.DisplayOrder, Array.Empty<CategoryTreeNodeDto>());
}

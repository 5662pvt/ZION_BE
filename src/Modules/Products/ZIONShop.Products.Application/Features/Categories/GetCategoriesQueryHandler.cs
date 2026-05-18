using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.Categories;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryTreeNodeDto>>>
{
    private readonly ICategoryRepository _categories;

    public GetCategoriesQueryHandler(ICategoryRepository categories) => _categories = categories;

    public async Task<Result<IReadOnlyList<CategoryTreeNodeDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var all = await _categories.Query().AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        IReadOnlyList<CategoryTreeNodeDto> tree = request.AsTree ? BuildTree(all, null) : all.Select(MapFlat).ToList();
        return Result.Success(tree);
    }

    private static IReadOnlyList<CategoryTreeNodeDto> BuildTree(List<Category> all, Guid? parentId) =>
        all.Where(c => c.ParentId == parentId)
            .Select(c => new CategoryTreeNodeDto(c.Id, c.Name, c.Slug, c.DisplayOrder, BuildTree(all, c.Id)))
            .ToList();

    private static CategoryTreeNodeDto MapFlat(Category c) =>
        new(c.Id, c.Name, c.Slug, c.DisplayOrder, Array.Empty<CategoryTreeNodeDto>());
}

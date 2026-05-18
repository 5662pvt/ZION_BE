namespace ZIONShop.Products.Application.DTOs;

public record CategoryDto(Guid Id, string Name, string Slug, string? Description, Guid? ParentId, int DisplayOrder);

public record CategoryTreeNodeDto(Guid Id, string Name, string Slug, int DisplayOrder, IReadOnlyList<CategoryTreeNodeDto> Children);

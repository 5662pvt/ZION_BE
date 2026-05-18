using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Products.Domain.Entities;

public class Category : AuditableEntity
{
    private Category() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ParentId { get; private set; }
    public int DisplayOrder { get; private set; }

    public static Category Create(string name, string slug, Guid? parentId = null, string? description = null, int displayOrder = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Slug = slug.Trim().ToLowerInvariant(),
        Description = description,
        ParentId = parentId,
        DisplayOrder = displayOrder
    };

    public void Update(string name, string? description, int displayOrder)
    {
        Name = name.Trim();
        Description = description;
        DisplayOrder = displayOrder;
    }
}

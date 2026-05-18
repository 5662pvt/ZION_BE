using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Products.Domain.Entities;

public class ProductImage : BaseEntity
{
    private ProductImage() { }

    public Guid ProductId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? Alt { get; private set; }
    public int DisplayOrder { get; private set; }

    public static ProductImage Create(Guid productId, string url, string? alt, int displayOrder) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = productId,
        Url = url,
        Alt = alt,
        DisplayOrder = displayOrder
    };
}

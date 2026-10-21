using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Products.Domain.Entities;

public class ProductVariant : BaseEntity
{
    private ProductVariant() { }

    public Guid ProductId { get; private set; }
    public Sku Sku { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public string? Option1Value { get; private set; }
    public string? Option2Value { get; private set; }
    public Guid? ImageId { get; private set; }
    public bool IsActive { get; private set; }
    public int DisplayOrder { get; private set; }

    public string Label => string.Join(" / ", new[] { Option1Value, Option2Value }.Where(v => !string.IsNullOrWhiteSpace(v)));

    public static ProductVariant Create(
        Guid id,
        Guid productId,
        Sku sku,
        Money price,
        string? option1Value,
        string? option2Value,
        Guid? imageId,
        bool isActive,
        int displayOrder) => new()
    {
        Id = id,
        ProductId = productId,
        Sku = sku,
        Price = price,
        Option1Value = option1Value,
        Option2Value = option2Value,
        ImageId = imageId,
        IsActive = isActive,
        DisplayOrder = displayOrder
    };

    internal void Update(Sku sku, Money price, string? option1Value, string? option2Value, Guid? imageId, bool isActive, int displayOrder)
    {
        Sku = sku;
        Price = price;
        Option1Value = option1Value;
        Option2Value = option2Value;
        ImageId = imageId;
        IsActive = isActive;
        DisplayOrder = displayOrder;
    }

    internal void UpdatePrice(Money price) => Price = price;

    internal void Deactivate() => IsActive = false;

    internal void ClearImage() => ImageId = null;
}

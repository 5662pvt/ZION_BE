using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Orders.Domain.Entities;

public class OrderLine : BaseEntity
{
    private OrderLine() { }

    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public bool IsGift { get; private set; }
    public Guid? GiftPromotionId { get; private set; }
    public Guid VariantId { get; private set; }
    public string? VariantLabel { get; private set; }
    public string? Sku { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    public static OrderLine Create(
        Guid orderId, Guid productId, string productName, decimal unitPrice, int quantity,
        Guid? variantId = null, string? variantLabel = null, string? sku = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive", nameof(quantity));
        if (unitPrice < 0) throw new ArgumentException("Unit price cannot be negative", nameof(unitPrice));

        return new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice,
            Quantity = quantity,
            VariantId = variantId ?? productId,
            VariantLabel = variantLabel,
            Sku = sku
        };
    }

    public static OrderLine CreateGift(
        Guid orderId, Guid productId, string productName, int quantity, Guid promotionId,
        Guid? variantId = null, string? variantLabel = null, string? sku = null)
    {
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive", nameof(quantity));

        return new OrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = productName,
            UnitPrice = 0m,
            Quantity = quantity,
            IsGift = true,
            GiftPromotionId = promotionId,
            VariantId = variantId ?? productId,
            VariantLabel = variantLabel,
            Sku = sku
        };
    }
}

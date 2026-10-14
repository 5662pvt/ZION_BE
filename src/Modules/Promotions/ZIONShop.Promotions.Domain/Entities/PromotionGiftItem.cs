namespace ZIONShop.Promotions.Domain.Entities;

/// <summary>Owned child collection of <see cref="Promotion"/> (table promotion_gift_items, PK (PromotionId, ProductId)).</summary>
public class PromotionGiftItem
{
    private PromotionGiftItem() { }

    public Guid PromotionId { get; private set; }
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    public static PromotionGiftItem Create(Guid promotionId, Guid productId, int quantity) => new()
    {
        PromotionId = promotionId,
        ProductId = productId,
        Quantity = quantity
    };
}

using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Inventory.Domain.Entities;

public class StockMovement : BaseEntity
{
    private StockMovement() { }

    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public string? Reference { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }

    public static StockMovement Create(Guid productId, Guid variantId, StockMovementType type, int quantity, string? reference, DateTime now, string? createdBy = null)
    {
        return new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            VariantId = variantId,
            Type = type,
            Quantity = quantity,
            Reference = reference,
            CreatedAt = now,
            CreatedBy = createdBy
        };
    }
}

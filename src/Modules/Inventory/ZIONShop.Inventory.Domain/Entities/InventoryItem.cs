using ZIONShop.Inventory.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Domain.Entities;

public class InventoryItem : AggregateRoot
{
    public const int DefaultLowStockThreshold = 5;

    private InventoryItem() { }

    public Guid ProductId { get; private set; }
    public Guid VariantId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public string? VariantLabel { get; private set; }
    public int OnHand { get; private set; }
    public int Reserved { get; private set; }
    public int LowStockThreshold { get; private set; } = DefaultLowStockThreshold;

    public int Available => OnHand - Reserved;
    public bool IsLowStock => Available <= LowStockThreshold;

    public static InventoryItem Create(Guid productId, Guid variantId, string sku, string productName, string? variantLabel, int onHand, int lowStockThreshold = DefaultLowStockThreshold)
    {
        return new InventoryItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            VariantId = variantId,
            Sku = sku,
            ProductName = productName,
            VariantLabel = string.IsNullOrWhiteSpace(variantLabel) ? null : variantLabel,
            OnHand = Math.Max(0, onHand),
            Reserved = 0,
            LowStockThreshold = Math.Max(0, lowStockThreshold)
        };
    }

    public Result Reserve(int quantity)
    {
        if (quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);
        if (Available < quantity) return Result.Failure(InventoryErrors.InsufficientStock);

        Reserved += quantity;
        return Result.Success();
    }

    public Result Release(int quantity)
    {
        if (quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);

        Reserved = Math.Max(0, Reserved - quantity);
        return Result.Success();
    }

    public Result Commit(int quantity)
    {
        if (quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);
        if (Reserved < quantity) return Result.Failure(InventoryErrors.InsufficientReserved);
        if (OnHand < quantity) return Result.Failure(InventoryErrors.InsufficientStock);

        OnHand -= quantity;
        Reserved -= quantity;
        return Result.Success();
    }

    public Result Return(int quantity)
    {
        if (quantity <= 0) return Result.Failure(InventoryErrors.InvalidQuantity);

        OnHand += quantity;
        return Result.Success();
    }

    public Result Adjust(int delta, string reason)
    {
        var newOnHand = OnHand + delta;
        if (newOnHand < 0) return Result.Failure(InventoryErrors.InvalidAdjustment);
        if (newOnHand < Reserved) return Result.Failure(InventoryErrors.AdjustmentBelowReserved);

        OnHand = newOnHand;
        return Result.Success();
    }

    /// <summary>Refreshes the denormalized product-name snapshot; returns false (no-op) when unchanged.</summary>
    public bool RenameProduct(string productName)
    {
        if (ProductName == productName) return false;
        ProductName = productName;
        return true;
    }

    /// <summary>Refreshes the denormalized SKU/variant-label snapshot; returns false (no-op) when unchanged.</summary>
    public bool RenameVariant(string sku, string? variantLabel)
    {
        var normalizedLabel = string.IsNullOrWhiteSpace(variantLabel) ? null : variantLabel;
        if (Sku == sku && VariantLabel == normalizedLabel) return false;

        Sku = sku;
        VariantLabel = normalizedLabel;
        return true;
    }
}

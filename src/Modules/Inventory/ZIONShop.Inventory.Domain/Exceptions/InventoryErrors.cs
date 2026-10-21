using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Domain.Exceptions;

public static class InventoryErrors
{
    public static readonly Error ItemNotFound = Error.NotFound("Inventory.ItemNotFound", "Inventory item not found");
    public static readonly Error ProductNotFound = Error.NotFound("Inventory.ProductNotFound", "Product not found");
    public static readonly Error VariantNotFound = Error.NotFound("Inventory.VariantNotFound", "Product variant not found");
    public static readonly Error VariantRequired = Error.Validation("Inventory.VariantRequired", "Product has multiple variants; a variantId must be specified");
    public static readonly Error InsufficientStock = Error.Conflict("Inventory.InsufficientStock", "Insufficient available stock");
    public static readonly Error InsufficientReserved = Error.Conflict("Inventory.InsufficientReserved", "Insufficient reserved stock");
    public static readonly Error InvalidQuantity = Error.Validation("Inventory.InvalidQuantity", "Quantity must be greater than zero");
    public static readonly Error InvalidAdjustment = Error.Validation("Inventory.InvalidAdjustment", "Adjustment would make stock negative");
    public static readonly Error AdjustmentBelowReserved = Error.Conflict("Inventory.AdjustmentBelowReserved", "Adjustment would drop stock below reserved quantity");
    public static readonly Error ConcurrencyConflict = Error.Conflict("Inventory.ConcurrencyConflict", "Stock was modified concurrently, please retry");
}

using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Application.Mappings;

public static class InventoryMappingExtensions
{
    public static InventoryItemDto ToDto(this InventoryItem item) => new(
        item.ProductId,
        item.VariantId,
        item.VariantLabel,
        item.Sku,
        item.ProductName,
        item.OnHand,
        item.Reserved,
        item.Available,
        item.LowStockThreshold,
        item.IsLowStock);

    public static StockMovementDto ToDto(this StockMovement movement) => new(
        movement.Id,
        movement.ProductId,
        movement.VariantId,
        movement.Type.ToString(),
        movement.Quantity,
        movement.Reference,
        movement.CreatedAt,
        movement.CreatedBy);
}

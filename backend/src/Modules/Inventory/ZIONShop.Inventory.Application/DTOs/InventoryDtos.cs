namespace ZIONShop.Inventory.Application.DTOs;

public record InventoryItemDto(
    Guid ProductId,
    string Sku,
    string ProductName,
    int OnHand,
    int Reserved,
    int Available,
    int LowStockThreshold,
    bool IsLowStock);

public record AvailabilityDto(Guid ProductId, int Available, bool InStock);

public record StockMovementDto(
    Guid Id,
    Guid ProductId,
    string Type,
    int Quantity,
    string? Reference,
    DateTime CreatedAt,
    string? CreatedBy);

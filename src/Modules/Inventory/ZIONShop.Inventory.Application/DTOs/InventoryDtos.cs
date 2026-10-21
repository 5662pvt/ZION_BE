namespace ZIONShop.Inventory.Application.DTOs;

public record InventoryItemDto(
    Guid ProductId,
    Guid VariantId,
    string? VariantLabel,
    string Sku,
    string ProductName,
    int OnHand,
    int Reserved,
    int Available,
    int LowStockThreshold,
    bool IsLowStock);

public record AvailabilityDto(Guid ProductId, Guid VariantId, string? VariantLabel, int Available, bool InStock);

/// <summary>Product-level aggregate (sum of Available across the product's variants) for <c>GET /inventory/availability/products</c>.</summary>
public record ProductAvailabilityDto(Guid ProductId, int Available, bool InStock);

public record StockMovementDto(
    Guid Id,
    Guid ProductId,
    Guid VariantId,
    string Type,
    int Quantity,
    string? Reference,
    DateTime CreatedAt,
    string? CreatedBy);

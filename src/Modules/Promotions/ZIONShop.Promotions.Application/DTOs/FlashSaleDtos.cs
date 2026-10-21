namespace ZIONShop.Promotions.Application.DTOs;

public record FlashSaleItemDto(Guid Id, Guid ProductId, Guid VariantId, string? VariantLabel, string? Sku, decimal FlashPrice, int AllocatedQuantity, int SoldQuantity, int Available);

public record FlashSaleSummaryDto(Guid Id, string Name, DateTime StartsAt, DateTime EndsAt, bool IsActive, int ItemCount);

public record FlashSaleDto(Guid Id, string Name, DateTime StartsAt, DateTime EndsAt, bool IsActive, IReadOnlyList<FlashSaleItemDto> Items);

public record ActiveFlashSaleItemDto(
    Guid ProductId,
    Guid VariantId,
    string? VariantLabel,
    string? Sku,
    string ProductName,
    string? ImageUrl,
    decimal FlashPrice,
    decimal RegularPrice,
    int Available,
    int AllocatedQuantity,
    DateTime EndsAt);

public record ActiveFlashSaleDto(Guid Id, string Name, DateTime StartsAt, DateTime EndsAt, IReadOnlyList<ActiveFlashSaleItemDto> Items);

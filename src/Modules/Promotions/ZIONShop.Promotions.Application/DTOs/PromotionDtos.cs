namespace ZIONShop.Promotions.Application.DTOs;

public record GiftItemDto(Guid ProductId, int Quantity);

public record PromotionSummaryDto(
    Guid Id,
    string Name,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal MinOrderSubtotal,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsActive,
    string Status,
    int? UsageLimit,
    int? PerUserLimit,
    int UsedCount,
    int CouponCount,
    string Scope,
    IReadOnlyList<Guid> TargetProductIds,
    IReadOnlyList<Guid> TargetCategoryIds,
    int? MinQuantity,
    IReadOnlyList<GiftItemDto> GiftItems);

public record PromotionDto(
    Guid Id,
    string Name,
    string? Description,
    string Type,
    decimal Value,
    decimal? MaxDiscountAmount,
    decimal MinOrderSubtotal,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsActive,
    string Status,
    int? UsageLimit,
    int? PerUserLimit,
    int UsedCount,
    int CouponCount,
    string Scope,
    IReadOnlyList<Guid> TargetProductIds,
    IReadOnlyList<Guid> TargetCategoryIds,
    IReadOnlyList<CouponDto> Coupons,
    int? MinQuantity,
    IReadOnlyList<GiftItemDto> GiftItems);

public record CouponDto(Guid Id, string Code, bool IsActive, int UsedCount, DateTime CreatedAt);

public record CouponValidationResultDto(string Code, string PromotionName, decimal DiscountAmount, string DiscountTarget);

public record GiftPreviewDto(Guid PromotionId, string PromotionName, Guid ProductId, string ProductName, string? ImageUrl, int Quantity, bool Available);

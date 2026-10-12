namespace ZIONShop.Promotions.Application.DTOs;

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
    int CouponCount);

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
    IReadOnlyList<CouponDto> Coupons);

public record CouponDto(Guid Id, string Code, bool IsActive, int UsedCount, DateTime CreatedAt);

public record CouponValidationResultDto(string Code, string PromotionName, decimal DiscountAmount, string DiscountTarget);

using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Application.Mappings;

public static class PromotionMappingExtensions
{
    public static PromotionSummaryDto ToSummaryDto(this Promotion promotion, int couponCount, DateTime utcNow) => new(
        promotion.Id,
        promotion.Name,
        promotion.Type.ToString(),
        promotion.Value,
        promotion.MaxDiscountAmount,
        promotion.MinOrderSubtotal,
        promotion.StartsAt,
        promotion.EndsAt,
        promotion.IsActive,
        promotion.GetStatus(utcNow).ToString(),
        promotion.UsageLimit,
        promotion.PerUserLimit,
        promotion.UsedCount,
        couponCount,
        promotion.Scope.ToString(),
        promotion.TargetProductIds,
        promotion.TargetCategoryIds,
        promotion.MinQuantity,
        promotion.GiftItems.Select(g => g.ToDto()).ToList());

    public static PromotionDto ToDto(this Promotion promotion, IReadOnlyList<CouponCode> coupons, DateTime utcNow) => new(
        promotion.Id,
        promotion.Name,
        promotion.Description,
        promotion.Type.ToString(),
        promotion.Value,
        promotion.MaxDiscountAmount,
        promotion.MinOrderSubtotal,
        promotion.StartsAt,
        promotion.EndsAt,
        promotion.IsActive,
        promotion.GetStatus(utcNow).ToString(),
        promotion.UsageLimit,
        promotion.PerUserLimit,
        promotion.UsedCount,
        coupons.Count,
        promotion.Scope.ToString(),
        promotion.TargetProductIds,
        promotion.TargetCategoryIds,
        coupons.Select(c => c.ToDto()).ToList(),
        promotion.MinQuantity,
        promotion.GiftItems.Select(g => g.ToDto()).ToList());

    public static CouponDto ToDto(this CouponCode coupon) => new(coupon.Id, coupon.Code, coupon.IsActive, coupon.UsedCount, coupon.CreatedAt);

    public static GiftItemDto ToDto(this PromotionGiftItem item) => new(item.ProductId, item.VariantId, item.Quantity);
}

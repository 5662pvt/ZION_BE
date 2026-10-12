using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Domain.Exceptions;

public static class PromotionsErrors
{
    public static readonly Error PromotionNotFound = Error.NotFound("Promotions.PromotionNotFound", "Khuyến mãi không tồn tại");
    public static readonly Error InvalidPromotionType = Error.Validation("Promotions.InvalidPromotionType", "'type' phải là 'PercentageOff' hoặc 'FixedAmountOff'");
    public static readonly Error CouponNotFound = Error.NotFound("Promotions.CouponNotFound", "Mã coupon không tồn tại");
    public static readonly Error AlreadyActive = Error.Conflict("Promotions.AlreadyActive", "Khuyến mãi đã được kích hoạt");
    public static readonly Error AlreadyInactive = Error.Conflict("Promotions.AlreadyInactive", "Khuyến mãi đã bị tắt");
    public static readonly Error CouponAlreadyInactive = Error.Conflict("Promotions.CouponAlreadyInactive", "Mã coupon đã bị tắt");
    public static readonly Error CouponCodeTaken = Error.Conflict("Promotions.CouponCodeTaken", "Mã coupon đã được sử dụng");
    public static readonly Error UsageLimitBelowUsedCount = Error.Validation("Promotions.UsageLimitBelowUsedCount", "Giới hạn sử dụng không được nhỏ hơn số lượt đã dùng");

    public static readonly Error CartEmpty = Error.Validation("Promotions.CartEmpty", "Giỏ hàng trống");

    public static readonly Error CouponNotExists = Error.Validation("Promotions.CouponNotExists", "Mã giảm giá không tồn tại");
    public static readonly Error CouponInactive = Error.Validation("Promotions.CouponInactive", "Mã giảm giá không còn hiệu lực");
    public static readonly Error CouponNotStarted = Error.Validation("Promotions.CouponNotStarted", "Mã giảm giá chưa có hiệu lực");
    public static readonly Error CouponExpired = Error.Validation("Promotions.CouponExpired", "Mã giảm giá đã hết hạn");
    public static readonly Error CouponUsageLimitReached = Error.Validation("Promotions.CouponUsageLimitReached", "Mã giảm giá đã hết lượt sử dụng");
    public static readonly Error CouponPerUserLimitReached = Error.Validation("Promotions.CouponPerUserLimitReached", "Bạn đã sử dụng hết lượt cho mã này");

    public static Error CouponMinSubtotalNotMet(decimal minOrderSubtotal) =>
        Error.Validation("Promotions.CouponMinSubtotalNotMet", $"Đơn hàng chưa đạt giá trị tối thiểu {minOrderSubtotal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}");
}

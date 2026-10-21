using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Domain.Exceptions;

public static class PromotionsErrors
{
    public static readonly Error PromotionNotFound = Error.NotFound("Promotions.PromotionNotFound", "Khuyến mãi không tồn tại");
    public static readonly Error InvalidPromotionType = Error.Validation("Promotions.InvalidPromotionType", "'type' phải là 'PercentageOff', 'FixedAmountOff' hoặc 'FreeShipping'");
    public static readonly Error InvalidPromotionScope = Error.Validation("Promotions.InvalidPromotionScope", "'scope' phải là 'Order', 'Products' hoặc 'Categories'");
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
    public static readonly Error CouponNotApplicable = Error.Validation("Promotions.CouponNotApplicable", "Mã giảm giá không áp dụng cho sản phẩm trong giỏ");
    public static readonly Error CouponAlreadyFreeShipping = Error.Validation("Promotions.CouponAlreadyFreeShipping", "Đơn hàng đã được miễn phí vận chuyển");

    public static Error CouponMinSubtotalNotMet(decimal minOrderSubtotal) =>
        Error.Validation("Promotions.CouponMinSubtotalNotMet", $"Đơn hàng chưa đạt giá trị tối thiểu {minOrderSubtotal.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)}");

    public static readonly Error GiftItemsRequired = Error.Validation("Promotions.GiftItemsRequired", "Khuyến mãi quà tặng phải có 1-5 sản phẩm quà tặng");
    public static readonly Error GiftItemsTooMany = Error.Validation("Promotions.GiftItemsTooMany", "Khuyến mãi quà tặng chỉ được tối đa 5 sản phẩm quà tặng");
    public static readonly Error GiftItemsDuplicateProduct = Error.Validation("Promotions.GiftItemsDuplicateProduct", "Sản phẩm quà tặng không được trùng lặp");
    public static readonly Error GiftQuantityInvalid = Error.Validation("Promotions.GiftQuantityInvalid", "Số lượng quà tặng phải trong khoảng 1-10");
    public static readonly Error GiftItemsNotAllowed = Error.Validation("Promotions.GiftItemsNotAllowed", "Chỉ khuyến mãi loại 'Gift' mới có sản phẩm quà tặng");
    public static readonly Error MinQuantityRequired = Error.Validation("Promotions.MinQuantityRequired", "'minQuantity' là bắt buộc khi scope khác 'Order'");
    public static readonly Error MinQuantityNotAllowedForOrderScope = Error.Validation("Promotions.MinQuantityNotAllowedForOrderScope", "'minQuantity' phải để trống khi scope là 'Order'");
    public static readonly Error OrderScopeRequiresPositiveSubtotal = Error.Validation("Promotions.OrderScopeRequiresPositiveSubtotal", "'minOrderSubtotal' phải lớn hơn 0 khi scope là 'Order' với khuyến mãi quà tặng");
    public static readonly Error MinQuantityNotAllowed = Error.Validation("Promotions.MinQuantityNotAllowed", "'minQuantity' chỉ áp dụng cho khuyến mãi loại 'Gift'");
    public static readonly Error GiftProductNotFound = Error.Validation("Promotions.GiftProductNotFound", "Sản phẩm quà tặng không tồn tại");
    public static readonly Error CouponNotAllowedForGift = Error.Validation("Promotions.CouponNotAllowedForGift", "Khuyến mãi quà tặng không dùng mã giảm giá");

    public static readonly Error FlashSaleNotFound = Error.NotFound("Promotions.FlashSaleNotFound", "Flash sale không tồn tại");
    public static readonly Error FlashSaleItemNotFound = Error.NotFound("Promotions.FlashSaleItemNotFound", "Sản phẩm flash sale không tồn tại");
    public static readonly Error FlashSaleProductNotFound = Error.Validation("Promotions.FlashSaleProductNotFound", "Sản phẩm không tồn tại");
    public static readonly Error FlashSaleItemProductAlreadyAdded = Error.Conflict("Promotions.FlashSaleItemProductAlreadyAdded", "Phân loại này đã có trong flash sale");
    public static readonly Error FlashSaleVariantRequired = Error.Validation("Promotions.FlashSaleVariantRequired", "Vui lòng chọn phân loại hàng");

    public static readonly Error GiftVariantRequired = Error.Validation("Promotions.GiftVariantRequired", "Vui lòng chọn phân loại hàng cho quà tặng");
}

using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Promotions.Domain.Entities;

public class CouponRedemption : BaseEntity
{
    private CouponRedemption() { }

    public Guid PromotionId { get; private set; }
    public Guid CouponCodeId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public RedemptionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static CouponRedemption Create(Guid promotionId, Guid couponCodeId, Guid orderId, Guid userId, decimal discountAmount, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        PromotionId = promotionId,
        CouponCodeId = couponCodeId,
        OrderId = orderId,
        UserId = userId,
        DiscountAmount = discountAmount,
        Status = RedemptionStatus.Reserved,
        CreatedAt = now
    };

    public void MarkCommitted(DateTime now)
    {
        Status = RedemptionStatus.Committed;
        UpdatedAt = now;
    }

    public void MarkReleased(DateTime now)
    {
        Status = RedemptionStatus.Released;
        UpdatedAt = now;
    }
}

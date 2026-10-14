using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Promotions.Domain.Entities;

public class GiftRedemption : BaseEntity
{
    private GiftRedemption() { }

    public Guid PromotionId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public RedemptionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static GiftRedemption Create(Guid promotionId, Guid orderId, Guid userId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        PromotionId = promotionId,
        OrderId = orderId,
        UserId = userId,
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

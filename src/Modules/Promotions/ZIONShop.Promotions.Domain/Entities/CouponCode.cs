using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Domain.Entities;

public class CouponCode : BaseEntity
{
    private CouponCode() { }

    public Guid PromotionId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public int UsedCount { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static CouponCode Create(Guid promotionId, string code, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        PromotionId = promotionId,
        Code = code,
        IsActive = true,
        UsedCount = 0,
        CreatedAt = now
    };

    public Result Deactivate()
    {
        if (!IsActive) return Result.Failure(PromotionsErrors.CouponAlreadyInactive);
        IsActive = false;
        return Result.Success();
    }

    public void IncrementUsage() => UsedCount++;

    public void DecrementUsage() => UsedCount = Math.Max(0, UsedCount - 1);
}

using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Promotions.Domain.Entities;

public class FlashSaleRedemption : BaseEntity
{
    private FlashSaleRedemption() { }

    public Guid FlashSaleItemId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitFlashPrice { get; private set; }
    public RedemptionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public static FlashSaleRedemption Create(Guid flashSaleItemId, Guid orderId, Guid userId, int quantity, decimal unitFlashPrice, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        FlashSaleItemId = flashSaleItemId,
        OrderId = orderId,
        UserId = userId,
        Quantity = quantity,
        UnitFlashPrice = unitFlashPrice,
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

using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Loyalty.Domain.Entities;

/// <summary>Marks that an order's earned points were revoked by a return/refund (unique per OrderId). Written
/// even if the order had not earned yet, so a late <c>OrderCompletedIntegrationEvent</c> earn is skipped instead
/// of racing the refund.</summary>
public class OrderPointsRevocation : BaseEntity
{
    private OrderPointsRevocation() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static OrderPointsRevocation Create(Guid orderId, Guid userId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        UserId = userId,
        CreatedAt = now
    };
}

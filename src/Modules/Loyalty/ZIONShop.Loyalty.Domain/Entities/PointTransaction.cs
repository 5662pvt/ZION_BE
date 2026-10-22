using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Loyalty.Domain.Entities;

/// <summary>Append-only ledger entry shown to the customer (<see cref="Description"/> is Vietnamese).</summary>
public class PointTransaction : BaseEntity
{
    private PointTransaction() { }

    public Guid UserId { get; private set; }
    public PointTransactionType Type { get; private set; }
    public int Points { get; private set; }
    public int BalanceAfter { get; private set; }
    public Guid? OrderId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public Guid? ActorId { get; private set; }

    public static PointTransaction Create(Guid userId, PointTransactionType type, int points, int balanceAfter, Guid? orderId, string description, DateTime now, Guid? actorId = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Type = type,
        Points = points,
        BalanceAfter = balanceAfter,
        OrderId = orderId,
        Description = description,
        CreatedAt = now,
        ActorId = actorId
    };
}

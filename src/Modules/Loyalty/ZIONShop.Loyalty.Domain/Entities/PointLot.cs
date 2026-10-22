using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Loyalty.Domain.Entities;

/// <summary>One earn batch of points, consumed FIFO by <see cref="PointReservationAllocation"/>s ordered by
/// <see cref="ExpiresAt"/> then <see cref="EarnedAt"/>. (UserId, Source, SourceRef) is unique for
/// <see cref="PointLotSource.Order"/>/<see cref="PointLotSource.Review"/> lots so earn handlers are idempotent.</summary>
public class PointLot : BaseEntity
{
    private PointLot() { }

    public Guid UserId { get; private set; }
    public PointLotSource Source { get; private set; }
    public string SourceRef { get; private set; } = string.Empty;
    public int Original { get; private set; }
    public int Remaining { get; private set; }
    public DateTime EarnedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    public static PointLot Create(Guid userId, PointLotSource source, string sourceRef, int points, DateTime earnedAt, DateTime expiresAt) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Source = source,
        SourceRef = sourceRef,
        Original = points,
        Remaining = points,
        EarnedAt = earnedAt,
        ExpiresAt = expiresAt
    };

    /// <summary>Consumes up to <paramref name="amount"/> from this lot (spend); returns how much was actually taken.</summary>
    public int Consume(int amount)
    {
        var taken = Math.Min(Remaining, Math.Max(0, amount));
        Remaining -= taken;
        return taken;
    }

    /// <summary>Gives back previously-spent points to this lot (release/refund of a live lot), never exceeding
    /// what was originally earned.</summary>
    public void AddBack(int amount) => Remaining = Math.Min(Original, Remaining + Math.Max(0, amount));

    /// <summary>Zeroes out whatever remains of this lot (expiry or order-return revocation); returns the amount removed.</summary>
    public int ZeroOut()
    {
        var removed = Math.Min(Remaining, Original);
        Remaining -= removed;
        return removed;
    }
}

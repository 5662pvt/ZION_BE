using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Loyalty.Domain.Entities;

/// <summary>One row per user, keyed by UserId (stored in the inherited <see cref="BaseEntity.Id"/>). Balance
/// is a cached sum of unexpired <see cref="PointLot.Remaining"/>, guarded by the PostgreSQL xmin concurrency
/// token so concurrent earns/spends on the same user retry instead of losing an update.</summary>
public class PointAccount : BaseEntity
{
    private PointAccount() { }

    public Guid UserId => Id;
    public int Balance { get; private set; }

    public static PointAccount Create(Guid userId) => new() { Id = userId, Balance = 0 };

    public void Increase(int points) => Balance += points;

    public void Decrease(int points) => Balance = Math.Max(0, Balance - points);
}

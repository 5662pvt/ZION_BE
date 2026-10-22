using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Loyalty.Domain.Entities;

/// <summary>One checkout's spent points (unique per OrderId — idempotency for
/// <see cref="ZIONShop.Contracts.Loyalty.ILoyaltyPointsService.ReserveAsync"/>). Reserved at checkout, Committed
/// when the order is confirmed, Released if the order never confirms, or Refunded if a confirmed order is later
/// returned.</summary>
public class PointReservation : BaseEntity
{
    private readonly List<PointReservationAllocation> _allocations = new();

    private PointReservation() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public int Points { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public PointReservationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public IReadOnlyList<PointReservationAllocation> Allocations => _allocations.AsReadOnly();

    public static PointReservation Create(Guid orderId, Guid userId, int points, decimal discountAmount, IReadOnlyList<(Guid LotId, int Points)> allocations, DateTime now)
    {
        var reservation = new PointReservation
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            UserId = userId,
            Points = points,
            DiscountAmount = discountAmount,
            Status = PointReservationStatus.Reserved,
            CreatedAt = now
        };

        foreach (var (lotId, lotPoints) in allocations)
            reservation._allocations.Add(PointReservationAllocation.Create(reservation.Id, lotId, lotPoints));

        return reservation;
    }

    public void MarkCommitted(DateTime now)
    {
        Status = PointReservationStatus.Committed;
        UpdatedAt = now;
    }

    public void MarkReleased(DateTime now)
    {
        Status = PointReservationStatus.Released;
        UpdatedAt = now;
    }

    public void MarkRefunded(DateTime now)
    {
        Status = PointReservationStatus.Refunded;
        UpdatedAt = now;
    }
}

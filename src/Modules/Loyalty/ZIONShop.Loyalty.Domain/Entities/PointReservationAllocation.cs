namespace ZIONShop.Loyalty.Domain.Entities;

/// <summary>How many points of a <see cref="PointReservation"/> came from which <see cref="PointLot"/> (FIFO
/// split); owned by the reservation, replayed on release/refund to know which lot to give points back to.</summary>
public class PointReservationAllocation
{
    private PointReservationAllocation() { }

    public Guid PointReservationId { get; private set; }
    public Guid LotId { get; private set; }
    public int Points { get; private set; }

    public static PointReservationAllocation Create(Guid pointReservationId, Guid lotId, int points) => new()
    {
        PointReservationId = pointReservationId,
        LotId = lotId,
        Points = points
    };
}

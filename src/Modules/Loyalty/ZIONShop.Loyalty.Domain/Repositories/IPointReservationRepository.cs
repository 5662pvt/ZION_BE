using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Domain.Repositories;

public interface IPointReservationRepository
{
    Task<PointReservation?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task AddAsync(PointReservation reservation, CancellationToken cancellationToken = default);

    void Update(PointReservation reservation);
}

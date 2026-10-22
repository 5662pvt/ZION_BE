using Microsoft.EntityFrameworkCore;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Loyalty.Infrastructure.Repositories;

public class PointReservationRepository : IPointReservationRepository
{
    private readonly LoyaltyDbContext _db;

    public PointReservationRepository(LoyaltyDbContext db) => _db = db;

    public Task<PointReservation?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.PointReservations.Include(r => r.Allocations).FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public async Task AddAsync(PointReservation reservation, CancellationToken cancellationToken = default) =>
        await _db.PointReservations.AddAsync(reservation, cancellationToken);

    public void Update(PointReservation reservation) => _db.PointReservations.Update(reservation);
}

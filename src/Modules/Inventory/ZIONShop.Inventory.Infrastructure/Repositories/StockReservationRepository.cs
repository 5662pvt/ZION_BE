using Microsoft.EntityFrameworkCore;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Repositories;

public class StockReservationRepository : IStockReservationRepository
{
    private readonly InventoryDbContext _db;

    public StockReservationRepository(InventoryDbContext db) => _db = db;

    public Task<List<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.StockReservations.Where(r => r.OrderId == orderId).ToListAsync(cancellationToken);

    public Task<List<StockReservation>> GetActiveExpiredAsync(DateTime asOf, int batchSize = 200, CancellationToken cancellationToken = default) =>
        _db.StockReservations
            .Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt != null && r.ExpiresAt <= asOf)
            .OrderBy(r => r.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(StockReservation reservation, CancellationToken cancellationToken = default) =>
        await _db.StockReservations.AddAsync(reservation, cancellationToken);
}

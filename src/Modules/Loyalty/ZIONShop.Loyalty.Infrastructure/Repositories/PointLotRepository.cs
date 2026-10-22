using Microsoft.EntityFrameworkCore;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Loyalty.Infrastructure.Repositories;

public class PointLotRepository : IPointLotRepository
{
    private readonly LoyaltyDbContext _db;

    public PointLotRepository(LoyaltyDbContext db) => _db = db;

    public Task<PointLot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.PointLots.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public Task<PointLot?> GetByUserSourceRefAsync(Guid userId, PointLotSource source, string sourceRef, CancellationToken cancellationToken = default) =>
        _db.PointLots.FirstOrDefaultAsync(l => l.UserId == userId && l.Source == source && l.SourceRef == sourceRef, cancellationToken);

    public Task<List<PointLot>> GetActiveForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default) =>
        _db.PointLots
            .Where(l => l.UserId == userId && l.Remaining > 0 && l.ExpiresAt > now)
            .OrderBy(l => l.ExpiresAt).ThenBy(l => l.EarnedAt)
            .ToListAsync(cancellationToken);

    public Task<List<PointLot>> GetExpiringSoonAsync(Guid userId, DateTime now, DateTime within, CancellationToken cancellationToken = default) =>
        _db.PointLots
            .Where(l => l.UserId == userId && l.Remaining > 0 && l.ExpiresAt > now && l.ExpiresAt <= within)
            .ToListAsync(cancellationToken);

    public Task<List<PointLot>> GetExpiredBatchAsync(DateTime now, int batchSize, CancellationToken cancellationToken = default) =>
        _db.PointLots
            .Where(l => l.Remaining > 0 && l.ExpiresAt <= now)
            .OrderBy(l => l.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PointLot lot, CancellationToken cancellationToken = default) =>
        await _db.PointLots.AddAsync(lot, cancellationToken);

    public void Update(PointLot lot) => _db.PointLots.Update(lot);
}

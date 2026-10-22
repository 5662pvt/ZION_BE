using Microsoft.EntityFrameworkCore;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Loyalty.Infrastructure.Repositories;

public class PointTransactionRepository : IPointTransactionRepository
{
    private readonly LoyaltyDbContext _db;

    public PointTransactionRepository(LoyaltyDbContext db) => _db = db;

    public async Task AddAsync(PointTransaction transaction, CancellationToken cancellationToken = default) =>
        await _db.PointTransactions.AddAsync(transaction, cancellationToken);

    public IQueryable<PointTransaction> Query() => _db.PointTransactions.AsNoTracking();
}

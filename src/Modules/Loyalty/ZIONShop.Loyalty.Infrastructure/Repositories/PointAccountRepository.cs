using Microsoft.EntityFrameworkCore;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Loyalty.Infrastructure.Repositories;

public class PointAccountRepository : IPointAccountRepository
{
    private readonly LoyaltyDbContext _db;

    public PointAccountRepository(LoyaltyDbContext db) => _db = db;

    public Task<PointAccount?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.PointAccounts.FirstOrDefaultAsync(a => a.Id == userId, cancellationToken);

    public async Task AddAsync(PointAccount account, CancellationToken cancellationToken = default) =>
        await _db.PointAccounts.AddAsync(account, cancellationToken);

    public void Update(PointAccount account) => _db.PointAccounts.Update(account);
}

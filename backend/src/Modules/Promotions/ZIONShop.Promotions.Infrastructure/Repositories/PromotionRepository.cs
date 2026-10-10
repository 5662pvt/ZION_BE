using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class PromotionRepository : IPromotionRepository
{
    private readonly PromotionsDbContext _db;

    public PromotionRepository(PromotionsDbContext db) => _db = db;

    public Task<Promotion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Promotions.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task AddAsync(Promotion promotion, CancellationToken cancellationToken = default) =>
        await _db.Promotions.AddAsync(promotion, cancellationToken);

    public void Update(Promotion promotion) => _db.Promotions.Update(promotion);

    public IQueryable<Promotion> Query() => _db.Promotions.AsQueryable();
}

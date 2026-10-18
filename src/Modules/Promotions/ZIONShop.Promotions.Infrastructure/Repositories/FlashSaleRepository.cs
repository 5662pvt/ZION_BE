using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class FlashSaleRepository : IFlashSaleRepository
{
    private readonly PromotionsDbContext _db;

    public FlashSaleRepository(PromotionsDbContext db) => _db = db;

    public Task<FlashSale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.FlashSales.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task AddAsync(FlashSale flashSale, CancellationToken cancellationToken = default) =>
        await _db.FlashSales.AddAsync(flashSale, cancellationToken);

    public void Update(FlashSale flashSale) => _db.FlashSales.Update(flashSale);

    public IQueryable<FlashSale> Query() => _db.FlashSales.AsQueryable();
}

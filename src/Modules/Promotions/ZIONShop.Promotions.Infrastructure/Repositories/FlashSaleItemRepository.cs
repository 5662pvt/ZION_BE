using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class FlashSaleItemRepository : IFlashSaleItemRepository
{
    private readonly PromotionsDbContext _db;

    public FlashSaleItemRepository(PromotionsDbContext db) => _db = db;

    public Task<FlashSaleItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.FlashSaleItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<List<FlashSaleItem>> GetByFlashSaleIdAsync(Guid flashSaleId, CancellationToken cancellationToken = default) =>
        _db.FlashSaleItems.Where(i => i.FlashSaleId == flashSaleId).ToListAsync(cancellationToken);

    public async Task AddAsync(FlashSaleItem item, CancellationToken cancellationToken = default) =>
        await _db.FlashSaleItems.AddAsync(item, cancellationToken);

    public void Update(FlashSaleItem item) => _db.FlashSaleItems.Update(item);

    public IQueryable<FlashSaleItem> Query() => _db.FlashSaleItems.AsQueryable();

    public async Task<bool> TryReserveAsync(Guid flashSaleItemId, int quantity, CancellationToken cancellationToken = default)
    {
        var rows = await _db.FlashSaleItems
            .Where(i => i.Id == flashSaleItemId && i.SoldQuantity + quantity <= i.AllocatedQuantity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.SoldQuantity, i => i.SoldQuantity + quantity), cancellationToken);

        return rows == 1;
    }

    public async Task ReleaseAsync(Guid flashSaleItemId, int quantity, CancellationToken cancellationToken = default)
    {
        await _db.FlashSaleItems
            .Where(i => i.Id == flashSaleItemId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                i => i.SoldQuantity,
                i => i.SoldQuantity - quantity < 0 ? 0 : i.SoldQuantity - quantity), cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;
using ZIONShop.Promotions.Infrastructure.Repositories;

namespace ZIONShop.Promotions.Tests.TestSupport;

/// <summary>
/// Delegates to the real <see cref="FlashSaleItemRepository"/> except for the atomic Try*Async methods,
/// which EF Core's InMemory provider cannot translate (<c>ExecuteUpdateAsync</c> is unsupported there --
/// Npgsql in production handles it fine; see the FlashSales concurrency integration test for real-provider
/// coverage). Reimplements the same invariants via tracked-entity load + immediate <c>SaveChangesAsync</c>,
/// mirroring ZIONShop.Inventory.Tests.TestSupport.InMemoryInventoryItemRepository.
/// </summary>
internal sealed class InMemoryFlashSaleItemRepository : IFlashSaleItemRepository
{
    private readonly PromotionsDbContext _db;
    private readonly FlashSaleItemRepository _inner;

    public InMemoryFlashSaleItemRepository(PromotionsDbContext db)
    {
        _db = db;
        _inner = new FlashSaleItemRepository(db);
    }

    public Task<FlashSaleItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _inner.GetByIdAsync(id, cancellationToken);

    public Task<List<FlashSaleItem>> GetByFlashSaleIdAsync(Guid flashSaleId, CancellationToken cancellationToken = default) =>
        _inner.GetByFlashSaleIdAsync(flashSaleId, cancellationToken);

    public Task AddAsync(FlashSaleItem item, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(item, cancellationToken);

    public void Update(FlashSaleItem item) => _inner.Update(item);

    public IQueryable<FlashSaleItem> Query() => _inner.Query();

    public async Task<bool> TryReserveAsync(Guid flashSaleItemId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.FlashSaleItems.FirstOrDefaultAsync(i => i.Id == flashSaleItemId, cancellationToken);
        if (item is null || !item.TryReserve(quantity)) return false;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task ReleaseAsync(Guid flashSaleItemId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.FlashSaleItems.FirstOrDefaultAsync(i => i.Id == flashSaleItemId, cancellationToken);
        if (item is null) return;

        item.Release(quantity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

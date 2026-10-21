using Microsoft.EntityFrameworkCore;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Repositories;

public class InventoryItemRepository : IInventoryItemRepository
{
    private readonly InventoryDbContext _db;

    public InventoryItemRepository(InventoryDbContext db) => _db = db;

    public Task<InventoryItem?> GetByVariantIdAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        _db.InventoryItems.FirstOrDefaultAsync(i => i.VariantId == variantId, cancellationToken);

    public async Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        await _db.InventoryItems.AddAsync(item, cancellationToken);

    public IQueryable<InventoryItem> Query() => _db.InventoryItems.AsQueryable();

    public async Task<bool> TryReserveAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var rows = await _db.InventoryItems
            .Where(i => i.VariantId == variantId && i.OnHand - i.Reserved >= quantity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Reserved, i => i.Reserved + quantity), cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryReleaseAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var rows = await _db.InventoryItems
            .Where(i => i.VariantId == variantId && i.Reserved >= quantity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Reserved, i => i.Reserved - quantity), cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryCommitAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var rows = await _db.InventoryItems
            .Where(i => i.VariantId == variantId && i.Reserved >= quantity && i.OnHand >= quantity)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.OnHand, i => i.OnHand - quantity)
                .SetProperty(i => i.Reserved, i => i.Reserved - quantity), cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryReturnAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var rows = await _db.InventoryItems
            .Where(i => i.VariantId == variantId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.OnHand, i => i.OnHand + quantity), cancellationToken);

        return rows == 1;
    }
}

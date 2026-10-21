using Microsoft.EntityFrameworkCore;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.Inventory.Infrastructure.Persistence;
using ZIONShop.Inventory.Infrastructure.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

/// <summary>
/// Delegates to the real <see cref="InventoryItemRepository"/> except for the atomic Try*Async methods,
/// which EF Core's InMemory provider cannot translate (<c>ExecuteUpdateAsync</c> is unsupported there --
/// Npgsql in production handles it fine, see <c>OversellTests</c> for real-provider concurrency coverage).
/// Reimplements the same invariants via tracked-entity load + immediate <c>SaveChangesAsync</c>, which is
/// safe for these single-threaded handler unit tests since they don't exercise concurrency themselves.
/// </summary>
internal sealed class InMemoryInventoryItemRepository : IInventoryItemRepository
{
    private readonly InventoryDbContext _db;
    private readonly InventoryItemRepository _inner;

    public InMemoryInventoryItemRepository(InventoryDbContext db)
    {
        _db = db;
        _inner = new InventoryItemRepository(db);
    }

    public Task<InventoryItem?> GetByVariantIdAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        _inner.GetByVariantIdAsync(variantId, cancellationToken);

    public Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(item, cancellationToken);

    public IQueryable<InventoryItem> Query() => _inner.Query();

    public async Task<bool> TryReserveAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.VariantId == variantId, cancellationToken);
        if (item is null || item.Reserve(quantity).IsFailure) return false;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryReleaseAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.VariantId == variantId, cancellationToken);
        if (item is null || item.Reserved < quantity) return false;

        item.Release(quantity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryCommitAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.VariantId == variantId, cancellationToken);
        if (item is null || item.Commit(quantity).IsFailure) return false;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryReturnAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.VariantId == variantId, cancellationToken);
        if (item is null || item.Return(quantity).IsFailure) return false;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

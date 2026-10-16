using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

/// <summary>
/// In-memory stand-in for <see cref="IInventoryItemRepository"/>. <see cref="TryReserveAsync"/> mimics
/// the real repository's atomic conditional UPDATE (no read-then-write window) and records what it
/// applied so a "transaction" (<see cref="FakeInventoryTransaction"/>) can roll every change in this
/// fake scope back atomically, the same way a real DB transaction rolling back undoes every
/// ExecuteUpdate issued on that connection.
/// </summary>
internal sealed class FakeInventoryItemRepository : IInventoryItemRepository
{
    private readonly Dictionary<Guid, InventoryItem> _items = new();
    private readonly List<(Guid ProductId, int Quantity)> _appliedInTransaction = new();

    /// <summary>Product ids for which TryReserveAsync should fail even if stock would allow it.</summary>
    public HashSet<Guid> ForceReserveFailure { get; } = new();

    /// <summary>Product ids for which TryReleaseAsync should fail even if the invariant would allow it.</summary>
    public HashSet<Guid> ForceReleaseFailure { get; } = new();

    /// <summary>Product ids for which TryCommitAsync should fail even if the invariant would allow it.</summary>
    public HashSet<Guid> ForceCommitFailure { get; } = new();

    /// <summary>Product ids for which TryReturnAsync should fail even if the invariant would allow it.</summary>
    public HashSet<Guid> ForceReturnFailure { get; } = new();

    public void Seed(params InventoryItem[] items)
    {
        foreach (var item in items) _items[item.ProductId] = item;
    }

    public InventoryItem? Get(Guid productId) => _items.GetValueOrDefault(productId);

    public Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.GetValueOrDefault(productId));

    public Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        _items[item.ProductId] = item;
        return Task.CompletedTask;
    }

    public IQueryable<InventoryItem> Query() => _items.Values.AsQueryable();

    public Task<bool> TryReserveAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (ForceReserveFailure.Contains(productId)) return Task.FromResult(false);
        if (!_items.TryGetValue(productId, out var item)) return Task.FromResult(false);

        var result = item.Reserve(quantity);
        if (result.IsFailure) return Task.FromResult(false);

        _appliedInTransaction.Add((productId, quantity));
        return Task.FromResult(true);
    }

    public Task<bool> TryReleaseAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (ForceReleaseFailure.Contains(productId)) return Task.FromResult(false);
        if (!_items.TryGetValue(productId, out var item) || item.Reserved < quantity) return Task.FromResult(false);

        item.Release(quantity);
        return Task.FromResult(true);
    }

    public Task<bool> TryCommitAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (ForceCommitFailure.Contains(productId)) return Task.FromResult(false);
        if (!_items.TryGetValue(productId, out var item)) return Task.FromResult(false);

        return Task.FromResult(item.Commit(quantity).IsSuccess);
    }

    public Task<bool> TryReturnAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (ForceReturnFailure.Contains(productId)) return Task.FromResult(false);
        if (!_items.TryGetValue(productId, out var item)) return Task.FromResult(false);

        return Task.FromResult(item.Return(quantity).IsSuccess);
    }

    internal void CommitTransaction() => _appliedInTransaction.Clear();

    internal void RollbackTransaction()
    {
        for (var i = _appliedInTransaction.Count - 1; i >= 0; i--)
        {
            var (productId, quantity) = _appliedInTransaction[i];
            _items[productId].Release(quantity);
        }

        _appliedInTransaction.Clear();
    }
}

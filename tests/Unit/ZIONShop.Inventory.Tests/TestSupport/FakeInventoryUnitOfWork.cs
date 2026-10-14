using ZIONShop.Inventory.Application.Interfaces;

namespace ZIONShop.Inventory.Tests.TestSupport;

internal sealed class FakeInventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly FakeInventoryItemRepository _items;

    public int SaveChangesCallCount { get; private set; }
    public int BeginTransactionCallCount { get; private set; }

    public FakeInventoryUnitOfWork(FakeInventoryItemRepository items) => _items = items;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }

    public Task<IInventoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        BeginTransactionCallCount++;
        return Task.FromResult<IInventoryTransaction>(new FakeInventoryTransaction(_items));
    }
}

/// <summary>
/// Stand-in for the real EF <c>IDbContextTransaction</c> wrapper. Disposing without a prior commit
/// rolls back, mirroring EF Core's own behavior for an un-committed explicit transaction.
/// </summary>
internal sealed class FakeInventoryTransaction : IInventoryTransaction
{
    private readonly FakeInventoryItemRepository _items;
    private bool _completed;

    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }

    public FakeInventoryTransaction(FakeInventoryItemRepository items) => _items = items;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (!_completed)
        {
            _items.CommitTransaction();
            Committed = true;
            _completed = true;
        }

        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (!_completed)
        {
            _items.RollbackTransaction();
            RolledBack = true;
            _completed = true;
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!_completed)
        {
            _items.RollbackTransaction();
            RolledBack = true;
            _completed = true;
        }

        return ValueTask.CompletedTask;
    }
}

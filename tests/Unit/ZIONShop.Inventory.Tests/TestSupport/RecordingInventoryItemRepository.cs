using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

/// <summary>
/// Wraps a <see cref="FakeInventoryItemRepository"/> and records the exact sequence of Try*Async calls
/// (operation + ProductId), so a test can assert the handler/processor issues them in ProductId order
/// regardless of the order reservations came back in from <c>GetByOrderIdAsync</c>/<c>GetActiveExpiredAsync</c>
/// -- the lock-ordering guarantee that avoids a Postgres deadlock (40P01) between two concurrent operations.
/// </summary>
internal sealed class RecordingInventoryItemRepository : IInventoryItemRepository
{
    private readonly FakeInventoryItemRepository _inner;

    public List<(string Operation, Guid ProductId)> Calls { get; } = new();

    public RecordingInventoryItemRepository(FakeInventoryItemRepository inner) => _inner = inner;

    public Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _inner.GetByProductIdAsync(productId, cancellationToken);

    public Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(item, cancellationToken);

    public IQueryable<InventoryItem> Query() => _inner.Query();

    public Task<bool> TryReserveAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Reserve", productId));
        return _inner.TryReserveAsync(productId, quantity, cancellationToken);
    }

    public Task<bool> TryReleaseAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Release", productId));
        return _inner.TryReleaseAsync(productId, quantity, cancellationToken);
    }

    public Task<bool> TryCommitAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Commit", productId));
        return _inner.TryCommitAsync(productId, quantity, cancellationToken);
    }

    public Task<bool> TryReturnAsync(Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Return", productId));
        return _inner.TryReturnAsync(productId, quantity, cancellationToken);
    }
}

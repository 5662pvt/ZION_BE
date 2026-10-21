using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;

namespace ZIONShop.Inventory.Tests.TestSupport;

/// <summary>
/// Wraps a <see cref="FakeInventoryItemRepository"/> and records the exact sequence of Try*Async calls
/// (operation + VariantId), so a test can assert the handler/processor issues them in VariantId order
/// regardless of the order reservations came back in from <c>GetByOrderIdAsync</c>/<c>GetActiveExpiredAsync</c>
/// -- the lock-ordering guarantee that avoids a Postgres deadlock (40P01) between two concurrent operations.
/// </summary>
internal sealed class RecordingInventoryItemRepository : IInventoryItemRepository
{
    private readonly FakeInventoryItemRepository _inner;

    public List<(string Operation, Guid VariantId)> Calls { get; } = new();

    public RecordingInventoryItemRepository(FakeInventoryItemRepository inner) => _inner = inner;

    public Task<InventoryItem?> GetByVariantIdAsync(Guid variantId, CancellationToken cancellationToken = default) =>
        _inner.GetByVariantIdAsync(variantId, cancellationToken);

    public Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        _inner.AddAsync(item, cancellationToken);

    public IQueryable<InventoryItem> Query() => _inner.Query();

    public Task<bool> TryReserveAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Reserve", variantId));
        return _inner.TryReserveAsync(variantId, quantity, cancellationToken);
    }

    public Task<bool> TryReleaseAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Release", variantId));
        return _inner.TryReleaseAsync(variantId, quantity, cancellationToken);
    }

    public Task<bool> TryCommitAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Commit", variantId));
        return _inner.TryCommitAsync(variantId, quantity, cancellationToken);
    }

    public Task<bool> TryReturnAsync(Guid variantId, int quantity, CancellationToken cancellationToken = default)
    {
        Calls.Add(("Return", variantId));
        return _inner.TryReturnAsync(variantId, quantity, cancellationToken);
    }
}

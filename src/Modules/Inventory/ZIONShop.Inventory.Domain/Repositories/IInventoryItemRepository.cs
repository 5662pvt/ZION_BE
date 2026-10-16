using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Domain.Repositories;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default);
    IQueryable<InventoryItem> Query();

    /// <summary>
    /// Atomically reserves <paramref name="quantity"/> for <paramref name="productId"/> with a single
    /// conditional UPDATE (OnHand - Reserved >= quantity), bypassing the change tracker so it is safe to
    /// call concurrently without loading/xmin conflicts. Returns false if no row matched (missing item or
    /// insufficient available stock) -- caller must not keep using a previously-loaded tracked instance for
    /// the same product afterward, since this does not update it.
    /// </summary>
    Task<bool> TryReserveAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically releases a previously-active reservation's <paramref name="quantity"/> for
    /// <paramref name="productId"/> with a single conditional UPDATE (Reserved - quantity, requires
    /// Reserved >= quantity), the same no-tracked-row shape as <see cref="TryReserveAsync"/> so release and
    /// reserve always take their row lock at the same instant they run, in caller-controlled order, instead
    /// of EF's own (unordered) batching at <c>SaveChangesAsync</c> time. Returns false if no row matched.
    /// </summary>
    Task<bool> TryReleaseAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically commits a previously-active reservation's <paramref name="quantity"/> for
    /// <paramref name="productId"/> with a single conditional UPDATE (OnHand -= quantity, Reserved -=
    /// quantity, requires both >= quantity), mirroring <see cref="TryReserveAsync"/>'s shape. Returns false
    /// if no row matched (missing item, or Reserved/OnHand below quantity).
    /// </summary>
    Task<bool> TryCommitAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically returns previously-committed <paramref name="quantity"/> to OnHand for
    /// <paramref name="productId"/> (e.g. a COD order cancelled after being confirmed/shipped). Always
    /// succeeds once the row exists; kept alongside <see cref="TryReleaseAsync"/> so a cancelled order's
    /// active and committed reservations can be processed as one ProductId-ordered pass without mixing in a
    /// tracked-entity write. Returns false if no row matched.
    /// </summary>
    Task<bool> TryReturnAsync(Guid productId, int quantity, CancellationToken cancellationToken = default);
}

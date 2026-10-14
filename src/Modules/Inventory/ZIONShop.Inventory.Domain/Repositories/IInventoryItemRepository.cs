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
}

using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface IFlashSaleItemRepository
{
    Task<FlashSaleItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<FlashSaleItem>> GetByFlashSaleIdAsync(Guid flashSaleId, CancellationToken cancellationToken = default);
    Task AddAsync(FlashSaleItem item, CancellationToken cancellationToken = default);
    void Update(FlashSaleItem item);
    IQueryable<FlashSaleItem> Query();

    /// <summary>
    /// Atomically reserves <paramref name="quantity"/> for <paramref name="flashSaleItemId"/> with a single
    /// conditional UPDATE (SoldQuantity + quantity &lt;= AllocatedQuantity), bypassing the change tracker so
    /// it is safe to call concurrently without loading/xmin conflicts -- mirrors
    /// IInventoryItemRepository.TryReserveAsync. Returns false if no row matched (missing item or the
    /// remaining quota can't cover the full quantity); caller must not partially reserve.
    /// </summary>
    Task<bool> TryReserveAsync(Guid flashSaleItemId, int quantity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically decrements SoldQuantity for <paramref name="flashSaleItemId"/> by <paramref name="quantity"/>,
    /// floored at 0. Idempotent-safe (no-op if the row is missing).
    /// </summary>
    Task ReleaseAsync(Guid flashSaleItemId, int quantity, CancellationToken cancellationToken = default);
}

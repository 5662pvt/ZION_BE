using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Domain.Repositories;

public interface IInventoryItemRepository
{
    Task<InventoryItem?> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
    Task AddAsync(InventoryItem item, CancellationToken cancellationToken = default);
    IQueryable<InventoryItem> Query();
}

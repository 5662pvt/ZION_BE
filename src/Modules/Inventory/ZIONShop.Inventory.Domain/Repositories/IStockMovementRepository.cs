using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Domain.Repositories;

public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default);
    IQueryable<StockMovement> Query();
}

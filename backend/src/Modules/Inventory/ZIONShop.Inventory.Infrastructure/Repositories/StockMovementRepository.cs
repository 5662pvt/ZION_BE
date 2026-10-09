using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly InventoryDbContext _db;

    public StockMovementRepository(InventoryDbContext db) => _db = db;

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default) =>
        await _db.StockMovements.AddAsync(movement, cancellationToken);

    public IQueryable<StockMovement> Query() => _db.StockMovements.AsQueryable();
}

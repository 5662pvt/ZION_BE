using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Readers;

public class InventoryStatsReader : IInventoryStatsReader
{
    private readonly InventoryDbContext _db;

    public InventoryStatsReader(InventoryDbContext db) => _db = db;

    public Task<int> CountLowStockAsync(CancellationToken ct = default) =>
        _db.InventoryItems.AsNoTracking().CountAsync(i => i.OnHand - i.Reserved <= i.LowStockThreshold, ct);
}

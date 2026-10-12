using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Readers;

public class StockAvailabilityReader : IStockAvailabilityReader
{
    private readonly InventoryDbContext _db;

    public StockAvailabilityReader(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var available = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .Select(i => new { i.ProductId, i.Available })
            .ToListAsync(ct);

        var result = ids.ToDictionary(id => id, _ => 0);
        foreach (var item in available)
            result[item.ProductId] = item.Available;

        return result;
    }
}

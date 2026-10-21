using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Readers;

public class ProductStockSummaryReader : IProductStockSummaryReader
{
    private readonly InventoryDbContext _db;

    public ProductStockSummaryReader(InventoryDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableByProductAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        var sums = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Available = g.Sum(i => i.OnHand - i.Reserved) })
            .ToListAsync(ct);

        var result = ids.ToDictionary(id => id, _ => 0);
        foreach (var sum in sums)
            result[sum.ProductId] = sum.Available;

        return result;
    }
}

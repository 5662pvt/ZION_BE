using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Inventory.Infrastructure.Readers;

public class ProductStockSummaryReader : IProductStockSummaryReader
{
    private readonly InventoryDbContext _db;
    private readonly IProductCatalogReader _catalog;

    public ProductStockSummaryReader(InventoryDbContext db, IProductCatalogReader catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetAvailableByProductAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => 0);
        if (ids.Count == 0) return result;

        var items = await _db.InventoryItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.ProductId))
            .Select(i => new { i.ProductId, i.VariantId, Available = i.OnHand - i.Reserved })
            .ToListAsync(ct);
        if (items.Count == 0) return result;

        // A product's available count must only include its ACTIVE variants -- a variant deactivated when
        // the product was converted to options still has an InventoryItems row, but it is no longer purchasable.
        var variantIds = items.Select(i => i.VariantId).Distinct().ToList();
        var variants = await _catalog.GetVariantsAsync(variantIds, ct);
        var activeVariantIds = variants.Where(v => v.IsActive).Select(v => v.VariantId).ToHashSet();

        foreach (var item in items.Where(i => activeVariantIds.Contains(i.VariantId)))
            result[item.ProductId] += item.Available;

        return result;
    }
}

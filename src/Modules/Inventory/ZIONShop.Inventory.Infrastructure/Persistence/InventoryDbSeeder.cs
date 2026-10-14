using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Infrastructure.Persistence;

public static class InventoryDbSeeder
{
    private const int DefaultSeedOnHand = 100;

    public static async Task SeedAsync(InventoryDbContext db, IProductCatalogReader catalog, CancellationToken cancellationToken = default)
    {
        var products = await catalog.ListAsync(cancellationToken);
        if (products.Count == 0) return;

        var existingProductIds = await db.InventoryItems.Select(i => i.ProductId).ToListAsync(cancellationToken);
        var existing = existingProductIds.ToHashSet();

        var missing = products.Where(p => !existing.Contains(p.ProductId)).ToList();
        if (missing.Count == 0) return;

        foreach (var product in missing)
            await db.InventoryItems.AddAsync(InventoryItem.Create(product.ProductId, product.Sku, product.Name, DefaultSeedOnHand), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}

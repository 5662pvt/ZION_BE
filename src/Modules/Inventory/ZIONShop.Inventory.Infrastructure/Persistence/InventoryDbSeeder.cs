using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Infrastructure.Persistence;

public static class InventoryDbSeeder
{
    private const int DefaultSeedOnHand = 100;

    public static async Task SeedAsync(InventoryDbContext db, IProductCatalogReader catalog, CancellationToken cancellationToken = default)
    {
        var variants = await catalog.ListVariantsAsync(cancellationToken);
        if (variants.Count == 0) return;

        var existingVariantIds = await db.InventoryItems.Select(i => i.VariantId).ToListAsync(cancellationToken);
        var existing = existingVariantIds.ToHashSet();

        var missing = variants.Where(v => !existing.Contains(v.VariantId)).ToList();
        if (missing.Count == 0) return;

        foreach (var variant in missing)
            await db.InventoryItems.AddAsync(
                InventoryItem.Create(variant.ProductId, variant.VariantId, variant.Sku, variant.ProductName, variant.VariantLabel, DefaultSeedOnHand),
                cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}

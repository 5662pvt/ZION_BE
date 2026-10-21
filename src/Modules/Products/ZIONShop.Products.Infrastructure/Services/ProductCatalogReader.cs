using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Products.Infrastructure.Persistence;

namespace ZIONShop.Products.Infrastructure.Services;

public class ProductCatalogReader : IProductCatalogReader
{
    private readonly ProductsDbContext _db;

    public ProductCatalogReader(ProductsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ProductCatalogItem>> ListAsync(CancellationToken ct = default)
        => await _db.Products.AsNoTracking()
            .Select(p => new ProductCatalogItem(
                p.Id,
                p.Variants.OrderBy(v => v.DisplayOrder).Select(v => v.Sku.Value).FirstOrDefault() ?? string.Empty,
                p.Name, p.Status.ToString(), p.CategoryId))
            .ToListAsync(ct);

    public async Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default)
        => await _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductCatalogItem(
                p.Id,
                p.Variants.OrderBy(v => v.DisplayOrder).Select(v => v.Sku.Value).FirstOrDefault() ?? string.Empty,
                p.Name, p.Status.ToString(), p.CategoryId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ProductVariantCatalogItem>> ListVariantsAsync(CancellationToken ct = default)
    {
        var rows = await _db.ProductVariants.AsNoTracking()
            .Join(_db.Products.AsNoTracking(), v => v.ProductId, p => p.Id, (v, p) => new VariantRow(
                v.Id, v.ProductId, v.Sku.Value, p.Name, v.Option1Value, v.Option2Value, v.IsActive, p.Status.ToString()))
            .ToListAsync(ct);

        return rows.Select(ToVariantCatalogItem).ToList();
    }

    public async Task<ProductVariantCatalogItem?> GetVariantAsync(Guid variantId, CancellationToken ct = default)
    {
        var row = await _db.ProductVariants.AsNoTracking()
            .Where(v => v.Id == variantId)
            .Join(_db.Products.AsNoTracking(), v => v.ProductId, p => p.Id, (v, p) => new VariantRow(
                v.Id, v.ProductId, v.Sku.Value, p.Name, v.Option1Value, v.Option2Value, v.IsActive, p.Status.ToString()))
            .FirstOrDefaultAsync(ct);

        return row is null ? null : ToVariantCatalogItem(row);
    }

    private static ProductVariantCatalogItem ToVariantCatalogItem(VariantRow row)
    {
        var label = string.Join(" / ", new[] { row.Option1Value, row.Option2Value }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return new ProductVariantCatalogItem(row.Id, row.ProductId, row.Sku, row.ProductName, label, row.IsActive, row.ProductStatus);
    }

    private sealed record VariantRow(Guid Id, Guid ProductId, string Sku, string ProductName, string? Option1Value, string? Option2Value, bool IsActive, string ProductStatus);
}

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
            .Select(p => new ProductCatalogItem(p.Id, p.Sku.Value, p.Name, p.Status.ToString()))
            .ToListAsync(ct);

    public async Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default)
        => await _db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new ProductCatalogItem(p.Id, p.Sku.Value, p.Name, p.Status.ToString()))
            .FirstOrDefaultAsync(ct);
}

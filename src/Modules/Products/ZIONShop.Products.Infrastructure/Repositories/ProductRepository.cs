using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Infrastructure.Persistence;

namespace ZIONShop.Products.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ProductsDbContext _db;

    public ProductRepository(ProductsDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Products.Include(p => p.Images).Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        _db.Products.Include(p => p.Images).Include(p => p.Variants).FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default) =>
        _db.ProductVariants.AnyAsync(v => v.Sku.Value == sku && (excludeProductId == null || v.ProductId != excludeProductId), cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        _db.Products.AnyAsync(p => p.Slug == slug, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await _db.Products.AddAsync(product, cancellationToken);

    public void Update(Product product) => _db.Products.Update(product);

    public void AddImage(ProductImage image) => _db.ProductImages.Add(image);

    public void AddVariant(ProductVariant variant) => _db.ProductVariants.Add(variant);

    public IQueryable<Product> Query() => _db.Products.AsQueryable();
}

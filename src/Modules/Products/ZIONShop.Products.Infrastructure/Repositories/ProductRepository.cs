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
        _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default) =>
        _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Sku.Value == sku, cancellationToken);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default) =>
        _db.Products.AnyAsync(p => p.Sku.Value == sku, cancellationToken);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) =>
        _db.Products.AnyAsync(p => p.Slug == slug, cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await _db.Products.AddAsync(product, cancellationToken);

    public void Update(Product product) => _db.Products.Update(product);

    public IQueryable<Product> Query() => _db.Products.AsQueryable();
}

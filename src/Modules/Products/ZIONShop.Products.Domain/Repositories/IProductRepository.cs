using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    void Update(Product product);
    void AddImage(ProductImage image);
    void AddVariant(ProductVariant variant);
    IQueryable<Product> Query();
}

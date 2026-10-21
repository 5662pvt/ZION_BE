using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Domain.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default);

    /// <summary>Batched version of <see cref="SkuExistsAsync"/>: returns the (normalized) SKUs among <paramref name="skus"/>
    /// that already belong to a variant of a DIFFERENT product (excludeProductId == null means any product).</summary>
    Task<IReadOnlyCollection<string>> GetConflictingSkusAsync(IReadOnlyCollection<string> skus, Guid? excludeProductId = null, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    void Update(Product product);
    void AddImage(ProductImage image);
    void AddVariant(ProductVariant variant);
    IQueryable<Product> Query();
}

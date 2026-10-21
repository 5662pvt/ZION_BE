namespace ZIONShop.Contracts.Products;

public record ProductCatalogItem(Guid ProductId, string Sku, string Name, string Status, Guid? CategoryId);

public record ProductVariantCatalogItem(Guid VariantId, Guid ProductId, string Sku, string ProductName, string VariantLabel, bool IsActive, string ProductStatus);

public interface IProductCatalogReader
{
    Task<IReadOnlyList<ProductCatalogItem>> ListAsync(CancellationToken ct = default);
    Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductVariantCatalogItem>> ListVariantsAsync(CancellationToken ct = default);
    Task<ProductVariantCatalogItem?> GetVariantAsync(Guid variantId, CancellationToken ct = default);
}

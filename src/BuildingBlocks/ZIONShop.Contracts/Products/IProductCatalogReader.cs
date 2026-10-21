namespace ZIONShop.Contracts.Products;

public record ProductCatalogItem(Guid ProductId, string Sku, string Name, string Status, Guid? CategoryId);

public record ProductVariantCatalogItem(Guid VariantId, Guid ProductId, string Sku, string ProductName, string VariantLabel, bool IsActive, string ProductStatus);

public interface IProductCatalogReader
{
    Task<IReadOnlyList<ProductCatalogItem>> ListAsync(CancellationToken ct = default);
    Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductVariantCatalogItem>> ListVariantsAsync(CancellationToken ct = default);

    /// <summary>Returns every variant of the product, active or not — callers that need only purchasable
    /// variants (e.g. flash sale/gift setup) must filter on <see cref="ProductVariantCatalogItem.IsActive"/> themselves.</summary>
    Task<ProductVariantCatalogItem?> GetVariantAsync(Guid variantId, CancellationToken ct = default);

    /// <summary>All variants (active or not) of a single product.</summary>
    Task<IReadOnlyList<ProductVariantCatalogItem>> ListVariantsByProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>Batched lookup of several variants by id (active or not).</summary>
    Task<IReadOnlyList<ProductVariantCatalogItem>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);
}

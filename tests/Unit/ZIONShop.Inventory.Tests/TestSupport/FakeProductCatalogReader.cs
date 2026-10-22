using ZIONShop.Contracts.Products;

namespace ZIONShop.Inventory.Tests.TestSupport;

internal sealed class FakeProductCatalogReader : IProductCatalogReader
{
    private readonly Dictionary<Guid, ProductCatalogItem> _items = new();
    private readonly Dictionary<Guid, ProductVariantCatalogItem> _variants = new();

    public FakeProductCatalogReader WithVariant(Guid variantId, Guid productId, string sku, string productName, string variantLabel, bool isActive = true, string productStatus = "Published")
    {
        _variants[variantId] = new ProductVariantCatalogItem(variantId, productId, sku, productName, variantLabel, isActive, productStatus);
        return this;
    }

    public Task<IReadOnlyList<ProductCatalogItem>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductCatalogItem>>(_items.Values.ToList());

    public Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default)
        => Task.FromResult(_items.TryGetValue(productId, out var item) ? item : null);

    public Task<IReadOnlyList<ProductVariantCatalogItem>> ListVariantsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductVariantCatalogItem>>(_variants.Values.ToList());

    public Task<ProductVariantCatalogItem?> GetVariantAsync(Guid variantId, CancellationToken ct = default)
        => Task.FromResult(_variants.TryGetValue(variantId, out var v) ? v : null);

    public Task<IReadOnlyList<ProductVariantCatalogItem>> ListVariantsByProductAsync(Guid productId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductVariantCatalogItem>>(_variants.Values.Where(v => v.ProductId == productId).ToList());

    public Task<IReadOnlyList<ProductVariantCatalogItem>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductVariantCatalogItem>>(_variants.Values.Where(v => variantIds.Contains(v.VariantId)).ToList());
}

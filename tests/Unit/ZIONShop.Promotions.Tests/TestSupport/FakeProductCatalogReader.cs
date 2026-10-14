using ZIONShop.Contracts.Products;

namespace ZIONShop.Promotions.Tests.TestSupport;

internal sealed class FakeProductCatalogReader : IProductCatalogReader
{
    private readonly Dictionary<Guid, ProductCatalogItem> _items = new();

    public FakeProductCatalogReader WithProduct(Guid productId, Guid? categoryId)
    {
        _items[productId] = new ProductCatalogItem(productId, "SKU", "Product", "Published", categoryId);
        return this;
    }

    public Task<IReadOnlyList<ProductCatalogItem>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProductCatalogItem>>(_items.Values.ToList());

    public Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default)
        => Task.FromResult(_items.TryGetValue(productId, out var item) ? item : null);
}

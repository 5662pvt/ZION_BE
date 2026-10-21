using ZIONShop.Contracts.Products;

namespace ZIONShop.Promotions.Tests.Fakes;

public class FakeProductPriceLookup : IProductPriceLookup
{
    private readonly Dictionary<Guid, ProductPriceInfo> _products = new();

    public FakeProductPriceLookup Add(Guid productId, string name, decimal unitPrice, string currency = "VND", bool isAvailable = true, string? slug = null, string? primaryImageUrl = null, Guid? variantId = null, string? variantLabel = null, string? sku = null)
    {
        _products[productId] = new ProductPriceInfo(productId, name, unitPrice, currency, isAvailable, slug ?? name, primaryImageUrl, variantId, variantLabel, sku);
        return this;
    }

    public Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default)
        => Task.FromResult(_products.TryGetValue(productId, out var info) ? info : null);

    public Task<IReadOnlyDictionary<Guid, ProductPriceInfo>> GetManyAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, ProductPriceInfo>>(
            _products.Where(kv => productIds.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value));

    public Task<IReadOnlyDictionary<Guid, ProductSummaryInfo>> GetProductSummaryAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, ProductSummaryInfo>>(
            _products.Where(kv => productIds.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => new ProductSummaryInfo(kv.Value.ProductId, kv.Value.Name, kv.Value.Slug, kv.Value.UnitPrice, kv.Value.Currency, kv.Value.PrimaryImageUrl, kv.Value.IsAvailable)));
}

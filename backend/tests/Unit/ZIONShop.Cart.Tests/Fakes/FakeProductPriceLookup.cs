using ZIONShop.Contracts.Products;

namespace ZIONShop.Cart.Tests.Fakes;

public class FakeProductPriceLookup : IProductPriceLookup
{
    private readonly Dictionary<Guid, ProductPriceInfo> _products = new();

    public FakeProductPriceLookup Add(Guid productId, string name, decimal unitPrice, string currency = "USD", bool isAvailable = true)
    {
        _products[productId] = new ProductPriceInfo(productId, name, unitPrice, currency, isAvailable);
        return this;
    }

    public Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default)
        => Task.FromResult(_products.TryGetValue(productId, out var info) ? info : null);
}

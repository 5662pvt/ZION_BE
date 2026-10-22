using ZIONShop.Contracts.Products;

namespace ZIONShop.Chat.Tests.Fakes;

internal sealed class FakeProductPriceLookup : IProductPriceLookup
{
    private readonly Dictionary<Guid, ProductSummaryInfo> _summaries = new();

    public FakeProductPriceLookup With(ProductSummaryInfo summary)
    {
        _summaries[summary.ProductId] = summary;
        return this;
    }

    public Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by Chat.");

    public Task<IReadOnlyDictionary<Guid, ProductPriceInfo>> GetManyAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by Chat.");

    public Task<IReadOnlyDictionary<Guid, ProductSummaryInfo>> GetProductSummaryAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        var result = productIds
            .Where(id => _summaries.ContainsKey(id))
            .ToDictionary(id => id, id => _summaries[id]);
        return Task.FromResult<IReadOnlyDictionary<Guid, ProductSummaryInfo>>(result);
    }
}

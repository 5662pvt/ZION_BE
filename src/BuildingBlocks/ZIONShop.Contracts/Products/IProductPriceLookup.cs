namespace ZIONShop.Contracts.Products;

public record ProductPriceInfo(Guid ProductId, string Name, decimal UnitPrice, string Currency, bool IsAvailable, string Slug, string? PrimaryImageUrl);

public interface IProductPriceLookup
{
    Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProductPriceInfo>> GetManyAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
}

namespace ZIONShop.Contracts.Products;

public record ProductPriceInfo(
    Guid ProductId,
    string Name,
    decimal UnitPrice,
    string Currency,
    bool IsAvailable,
    string Slug,
    string? PrimaryImageUrl,
    Guid? VariantId = null,
    string? VariantLabel = null,
    string? Sku = null);

public record ProductSummaryInfo(Guid ProductId, string Name, string Slug, decimal MinPrice, string Currency, string? PrimaryImageUrl, bool IsPublished);

public interface IProductPriceLookup
{
    /// <summary>
    /// productId may be EITHER a ProductVariant Id or a Product Id. If it is a variant, that variant is
    /// returned (IsAvailable = product Published &amp;&amp; variant active). If it is a product with exactly one
    /// active variant, that variant is returned. If it is a product with zero or several active variants,
    /// IsAvailable is false (a concrete variant must be chosen). Convention for callers: VariantId ?? ProductId
    /// is the stock/price key.
    /// </summary>
    Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, ProductPriceInfo>> GetManyAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);

    /// <summary>Product-level summary (MinPrice across active variants) for consumers that only need product identity, not a specific variant (e.g. wishlist, reviews).</summary>
    Task<IReadOnlyDictionary<Guid, ProductSummaryInfo>> GetProductSummaryAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);
}

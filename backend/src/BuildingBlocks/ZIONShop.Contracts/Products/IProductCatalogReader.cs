namespace ZIONShop.Contracts.Products;

public record ProductCatalogItem(Guid ProductId, string Sku, string Name, string Status);

public interface IProductCatalogReader
{
    Task<IReadOnlyList<ProductCatalogItem>> ListAsync(CancellationToken ct = default);
    Task<ProductCatalogItem?> GetAsync(Guid productId, CancellationToken ct = default);
}

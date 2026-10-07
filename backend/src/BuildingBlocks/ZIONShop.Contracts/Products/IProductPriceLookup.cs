namespace ZIONShop.Contracts.Products;

public record ProductPriceInfo(Guid ProductId, string Name, decimal UnitPrice, string Currency, bool IsAvailable);

public interface IProductPriceLookup
{
    Task<ProductPriceInfo?> GetAsync(Guid productId, CancellationToken cancellationToken = default);
}

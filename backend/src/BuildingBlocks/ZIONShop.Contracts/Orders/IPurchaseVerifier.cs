namespace ZIONShop.Contracts.Orders;

public interface IPurchaseVerifier
{
    /// <summary>Latest Completed order for this user containing this product, or null if none.</summary>
    Task<Guid?> GetCompletedOrderIdAsync(Guid userId, Guid productId, CancellationToken ct = default);
}

namespace ZIONShop.Contracts.Inventory;

public interface IInventoryStatsReader
{
    /// <summary>Same predicate as SearchStockQuery's lowStockOnly: OnHand − Reserved ≤ LowStockThreshold.</summary>
    Task<int> CountLowStockAsync(CancellationToken ct = default);
}

namespace ZIONShop.Contracts.Inventory;

public interface IProductStockSummaryReader
{
    /// <summary>Available (on hand − reserved) summed across every variant of each product; products without inventory rows → 0. AsNoTracking.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAvailableByProductAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
}

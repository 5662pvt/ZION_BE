namespace ZIONShop.Contracts.Inventory;

public interface IStockAvailabilityReader
{
    /// <summary>Available (on hand − reserved) per product; products without an inventory item → 0. AsNoTracking.</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetAvailableAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);
}

using ZIONShop.Contracts.Inventory;

namespace ZIONShop.Promotions.Tests.Fakes;

public class FakeStockAvailabilityReader : IStockAvailabilityReader
{
    private readonly Dictionary<Guid, int> _available = new();

    public FakeStockAvailabilityReader WithAvailable(Guid productId, int available)
    {
        _available[productId] = available;
        return this;
    }

    public Task<IReadOnlyDictionary<Guid, int>> GetAvailableAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(
            productIds.ToDictionary(id => id, id => _available.TryGetValue(id, out var qty) ? qty : 0));
}

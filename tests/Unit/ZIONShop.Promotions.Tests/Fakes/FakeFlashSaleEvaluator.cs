using ZIONShop.Contracts.Promotions;

namespace ZIONShop.Promotions.Tests.Fakes;

public class FakeFlashSaleEvaluator : IFlashSaleEvaluator
{
    public List<Guid> ReleasedOrderIds { get; } = new();

    public Task<IReadOnlyList<FlashSaleQuote>> QuoteAsync(IReadOnlyList<FlashSaleLine> lines, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FlashSaleQuote>>(Array.Empty<FlashSaleQuote>());

    public Task<IReadOnlyList<FlashSaleQuote>> ReserveAsync(IReadOnlyList<FlashSaleLine> lines, Guid orderId, Guid userId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FlashSaleQuote>>(Array.Empty<FlashSaleQuote>());

    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }
}

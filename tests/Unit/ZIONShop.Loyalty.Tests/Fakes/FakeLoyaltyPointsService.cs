using ZIONShop.Contracts.Loyalty;

namespace ZIONShop.Loyalty.Tests.Fakes;

internal sealed class FakeLoyaltyPointsService : ILoyaltyPointsService
{
    public List<Guid> ReleasedOrderIds { get; } = new();

    public Task<int> GetBalanceAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(0);

    public Task<int> QuoteAsync(Guid userId, decimal eligibleAmount, CancellationToken ct = default) => Task.FromResult(0);

    public Task<(LoyaltyRedemption? Redemption, LoyaltyError? Error)> ReserveAsync(Guid userId, Guid orderId, decimal eligibleAmount, CancellationToken ct = default) =>
        Task.FromResult<(LoyaltyRedemption?, LoyaltyError?)>((null, null));

    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }
}

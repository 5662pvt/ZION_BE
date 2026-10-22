using ZIONShop.Contracts.Loyalty;

namespace ZIONShop.Orders.Tests.Fakes;

public class FakeLoyaltyPointsService : ILoyaltyPointsService
{
    private LoyaltyRedemption? _nextRedemption;
    private LoyaltyError? _nextError;

    public List<(Guid UserId, Guid OrderId, decimal EligibleAmount)> ReserveCalls { get; } = new();
    public List<Guid> ReleasedOrderIds { get; } = new();

    public FakeLoyaltyPointsService WithRedemption(int points, decimal discountAmount)
    {
        _nextRedemption = new LoyaltyRedemption(Guid.Empty, points, discountAmount);
        return this;
    }

    public FakeLoyaltyPointsService WithError(string code, string message)
    {
        _nextError = new LoyaltyError(code, message);
        return this;
    }

    public Task<int> GetBalanceAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(0);

    public Task<int> QuoteAsync(Guid userId, decimal eligibleAmount, CancellationToken ct = default) => Task.FromResult(0);

    public Task<(LoyaltyRedemption? Redemption, LoyaltyError? Error)> ReserveAsync(Guid userId, Guid orderId, decimal eligibleAmount, CancellationToken ct = default)
    {
        ReserveCalls.Add((userId, orderId, eligibleAmount));
        if (_nextError is not null) return Task.FromResult<(LoyaltyRedemption?, LoyaltyError?)>((null, _nextError));
        if (_nextRedemption is not null)
            return Task.FromResult<(LoyaltyRedemption?, LoyaltyError?)>((_nextRedemption with { OrderId = orderId }, null));
        return Task.FromResult<(LoyaltyRedemption?, LoyaltyError?)>((null, null));
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }
}

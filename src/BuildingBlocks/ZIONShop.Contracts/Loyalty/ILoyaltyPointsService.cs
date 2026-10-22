namespace ZIONShop.Contracts.Loyalty;

public sealed record LoyaltyRedemption(Guid OrderId, int Points, decimal DiscountAmount);

public sealed record LoyaltyError(string Code, string Message);

public interface ILoyaltyPointsService
{
    Task<int> GetBalanceAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Max points usable for eligibleAmount right now (min(balance, floor(eligible × MaxRedeemPercent/100))).</summary>
    Task<int> QuoteAsync(Guid userId, decimal eligibleAmount, CancellationToken ct = default);

    /// <summary>Reserves min(balance, cap) points for the order. Idempotent per orderId (returns the existing reservation).
    /// Returns (null, null) when 0 points are usable (nothing reserved).</summary>
    Task<(LoyaltyRedemption? Redemption, LoyaltyError? Error)> ReserveAsync(Guid userId, Guid orderId, decimal eligibleAmount, CancellationToken ct = default);

    /// <summary>Idempotent; no-op if nothing reserved or already released/committed-and-refunded.</summary>
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);
}

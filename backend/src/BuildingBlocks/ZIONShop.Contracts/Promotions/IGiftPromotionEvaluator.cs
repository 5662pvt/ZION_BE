namespace ZIONShop.Contracts.Promotions;

public sealed record GiftRequest(Guid UserId, IReadOnlyList<CouponLine> Lines, decimal Subtotal);

public sealed record GiftGrant(Guid PromotionId, string PromotionName, Guid ProductId, int Quantity);

public interface IGiftPromotionEvaluator
{
    /// <summary>All gift grants the request qualifies for (no side effects).</summary>
    Task<IReadOnlyList<GiftGrant>> QuoteGiftsAsync(GiftRequest request, CancellationToken ct = default);

    /// <summary>Re-validates ONLY the given promotions, increments usage, stores GiftRedemption(Reserved) per promotion; idempotent per (promotionId, orderId); returns grants actually reserved.</summary>
    Task<IReadOnlyList<GiftGrant>> ReserveGiftsAsync(GiftRequest request, Guid orderId, IReadOnlyCollection<Guid> promotionIds, CancellationToken ct = default);

    /// <summary>Releases all Reserved/Committed gift redemptions of the order (decrement usage); no-op if none.</summary>
    Task ReleaseGiftsAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Releases one promotion's gift redemption for the order (gift dropped by inventory).</summary>
    Task ReleaseGiftAsync(Guid orderId, Guid promotionId, CancellationToken ct = default);
}

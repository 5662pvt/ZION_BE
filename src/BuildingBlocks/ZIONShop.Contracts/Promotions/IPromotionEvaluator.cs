namespace ZIONShop.Contracts.Promotions;

public sealed record CouponLine(Guid ProductId, int Quantity, decimal LineTotal);

public sealed record CouponRequest(string Code, Guid UserId, IReadOnlyList<CouponLine> Lines, decimal Subtotal, decimal ShippingFee);

public sealed record CouponQuote(string Code, Guid PromotionId, string PromotionName, decimal DiscountAmount, string DiscountTarget);

public sealed record CouponError(string Code, string Message);

public interface IPromotionEvaluator
{
    /// <summary>Read-only quote for the storefront preview and for Orders before reserving.</summary>
    Task<(CouponQuote? Quote, CouponError? Error)> QuoteAsync(CouponRequest request, CancellationToken ct = default);

    /// <summary>Atomically reserves one usage for this order (idempotent per orderId). Returns the same shapes.</summary>
    Task<(CouponQuote? Quote, CouponError? Error)> ReserveAsync(CouponRequest request, Guid orderId, CancellationToken ct = default);

    /// <summary>Releases a reservation made for orderId (idempotent; no-op if none).</summary>
    Task ReleaseAsync(Guid orderId, CancellationToken ct = default);
}

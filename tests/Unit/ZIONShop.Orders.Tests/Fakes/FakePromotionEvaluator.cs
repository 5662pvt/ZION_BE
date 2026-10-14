using ZIONShop.Contracts.Promotions;

namespace ZIONShop.Orders.Tests.Fakes;

public class FakePromotionEvaluator : IPromotionEvaluator
{
    private readonly Dictionary<string, CouponQuote> _quotes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CouponError> _errors = new(StringComparer.OrdinalIgnoreCase);

    public List<(string Code, Guid UserId, Guid OrderId, decimal Subtotal)> ReserveCalls { get; } = new();
    public List<Guid> ReleasedOrderIds { get; } = new();

    public FakePromotionEvaluator WithQuote(string code, decimal discountAmount, string promotionName = "Promo", Guid? promotionId = null, string discountTarget = "Items")
    {
        _quotes[code] = new CouponQuote(code, promotionId ?? Guid.NewGuid(), promotionName, discountAmount, discountTarget);
        return this;
    }

    public FakePromotionEvaluator WithError(string code, string message)
    {
        _errors[code] = new CouponError(code, message);
        return this;
    }

    public Task<(CouponQuote? Quote, CouponError? Error)> QuoteAsync(CouponRequest request, CancellationToken ct = default)
        => Task.FromResult(Resolve(request.Code));

    public Task<(CouponQuote? Quote, CouponError? Error)> ReserveAsync(CouponRequest request, Guid orderId, CancellationToken ct = default)
    {
        ReserveCalls.Add((request.Code, request.UserId, orderId, request.Subtotal));
        return Task.FromResult(Resolve(request.Code));
    }

    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }

    private (CouponQuote? Quote, CouponError? Error) Resolve(string code)
    {
        if (_quotes.TryGetValue(code, out var quote)) return (quote, null);
        if (_errors.TryGetValue(code, out var error)) return (null, error);
        return (null, new CouponError(code, "Mã giảm giá không tồn tại"));
    }
}

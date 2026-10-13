using ZIONShop.Contracts.Promotions;

namespace ZIONShop.Promotions.Tests.Fakes;

public class FakePromotionEvaluator : IPromotionEvaluator
{
    public List<Guid> ReleasedOrderIds { get; } = new();

    public Task<(CouponQuote? Quote, CouponError? Error)> QuoteAsync(CouponRequest request, CancellationToken ct = default) =>
        Task.FromResult<(CouponQuote?, CouponError?)>((null, new CouponError("Promotions.CouponNotExists", "Mã giảm giá không tồn tại")));

    public Task<(CouponQuote? Quote, CouponError? Error)> ReserveAsync(CouponRequest request, Guid orderId, CancellationToken ct = default) =>
        Task.FromResult<(CouponQuote?, CouponError?)>((null, new CouponError("Promotions.CouponNotExists", "Mã giảm giá không tồn tại")));

    public Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }
}

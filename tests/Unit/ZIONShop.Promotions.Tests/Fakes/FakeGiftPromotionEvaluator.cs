using ZIONShop.Contracts.Promotions;

namespace ZIONShop.Promotions.Tests.Fakes;

public class FakeGiftPromotionEvaluator : IGiftPromotionEvaluator
{
    private readonly List<GiftGrant> _grants = new();

    public List<Guid> ReleasedGiftsOrderIds { get; } = new();
    public List<(Guid OrderId, Guid PromotionId)> ReleasedGiftOrderPromotionIds { get; } = new();

    public FakeGiftPromotionEvaluator WithGrant(Guid promotionId, string promotionName, Guid productId, int quantity)
    {
        _grants.Add(new GiftGrant(promotionId, promotionName, productId, quantity));
        return this;
    }

    public Task<IReadOnlyList<GiftGrant>> QuoteGiftsAsync(GiftRequest request, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GiftGrant>>(_grants.ToList());

    public Task<IReadOnlyList<GiftGrant>> ReserveGiftsAsync(GiftRequest request, Guid orderId, IReadOnlyCollection<Guid> promotionIds, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GiftGrant>>(_grants.Where(g => promotionIds.Contains(g.PromotionId)).ToList());

    public Task ReleaseGiftsAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedGiftsOrderIds.Add(orderId);
        return Task.CompletedTask;
    }

    public Task ReleaseGiftAsync(Guid orderId, Guid promotionId, CancellationToken ct = default)
    {
        ReleasedGiftOrderPromotionIds.Add((orderId, promotionId));
        return Task.CompletedTask;
    }
}

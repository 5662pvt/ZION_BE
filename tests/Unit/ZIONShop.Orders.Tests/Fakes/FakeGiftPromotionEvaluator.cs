using ZIONShop.Contracts.Promotions;

namespace ZIONShop.Orders.Tests.Fakes;

public class FakeGiftPromotionEvaluator : IGiftPromotionEvaluator
{
    private readonly List<GiftGrant> _grants = new();
    private readonly HashSet<Guid> _reserveFailures = new();
    private bool _reserveGiftsThrows;

    public List<(Guid OrderId, List<Guid> PromotionIds)> ReserveCalls { get; } = new();
    public List<Guid> ReleasedOrderIds { get; } = new();
    public List<(Guid OrderId, Guid PromotionId)> ReleasedGiftCalls { get; } = new();

    public FakeGiftPromotionEvaluator WithGrant(Guid promotionId, Guid productId, int quantity, string promotionName = "Quà tặng", Guid? variantId = null)
    {
        _grants.Add(new GiftGrant(promotionId, promotionName, productId, quantity, variantId));
        return this;
    }

    /// <summary>Marks a promotion as failing re-validation on ReserveGiftsAsync (dropped at reserve time).</summary>
    public FakeGiftPromotionEvaluator WithReserveFailure(Guid promotionId)
    {
        _reserveFailures.Add(promotionId);
        return this;
    }

    /// <summary>Simulates ReserveGiftsAsync throwing (e.g. a DB error), to test reservation-release-on-exception paths.</summary>
    public FakeGiftPromotionEvaluator WithReserveGiftsException()
    {
        _reserveGiftsThrows = true;
        return this;
    }

    public Task<IReadOnlyList<GiftGrant>> QuoteGiftsAsync(GiftRequest request, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GiftGrant>>(_grants.ToList());

    public Task<IReadOnlyList<GiftGrant>> ReserveGiftsAsync(GiftRequest request, Guid orderId, IReadOnlyCollection<Guid> promotionIds, CancellationToken ct = default)
    {
        ReserveCalls.Add((orderId, promotionIds.ToList()));
        if (_reserveGiftsThrows) throw new InvalidOperationException("Simulated gift reserve failure");
        var reserved = _grants.Where(g => promotionIds.Contains(g.PromotionId) && !_reserveFailures.Contains(g.PromotionId)).ToList();
        return Task.FromResult<IReadOnlyList<GiftGrant>>(reserved);
    }

    public Task ReleaseGiftsAsync(Guid orderId, CancellationToken ct = default)
    {
        ReleasedOrderIds.Add(orderId);
        return Task.CompletedTask;
    }

    public Task ReleaseGiftAsync(Guid orderId, Guid promotionId, CancellationToken ct = default)
    {
        ReleasedGiftCalls.Add((orderId, promotionId));
        return Task.CompletedTask;
    }
}

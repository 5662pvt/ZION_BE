namespace ZIONShop.Contracts.Promotions;

public sealed record PromotionRef(Guid PromotionId, string Name, string Type);

public interface IPromotionLookup
{
    /// <summary>Resolves coupon codes (case-insensitive match against the stored CouponCode) to their promotion. Key = code as given.</summary>
    Task<IReadOnlyDictionary<string, PromotionRef>> ResolveCouponCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, PromotionRef>> GetByIdsAsync(IReadOnlyCollection<Guid> promotionIds, CancellationToken ct = default);
}

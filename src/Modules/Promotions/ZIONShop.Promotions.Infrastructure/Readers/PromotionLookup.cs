using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Readers;

public class PromotionLookup : IPromotionLookup
{
    private readonly PromotionsDbContext _db;

    public PromotionLookup(PromotionsDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, PromotionRef>> ResolveCouponCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var distinctCodes = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (distinctCodes.Count == 0) return new Dictionary<string, PromotionRef>();

        var upperCodes = distinctCodes.Select(c => c.ToUpperInvariant()).ToList();
        var rows = await _db.CouponCodes.AsNoTracking()
            .Where(c => upperCodes.Contains(c.Code.ToUpper()))
            .Join(_db.Promotions.AsNoTracking(), c => c.PromotionId, p => p.Id, (c, p) => new { c.Code, p.Id, p.Name, p.Type })
            .ToListAsync(ct);

        var byUpperCode = rows.ToDictionary(
            r => r.Code.ToUpperInvariant(),
            r => new PromotionRef(r.Id, r.Name, r.Type.ToString()));

        var result = new Dictionary<string, PromotionRef>();
        foreach (var code in distinctCodes)
            if (byUpperCode.TryGetValue(code.ToUpperInvariant(), out var promotionRef))
                result[code] = promotionRef;

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, PromotionRef>> GetByIdsAsync(IReadOnlyCollection<Guid> promotionIds, CancellationToken ct = default)
    {
        var ids = promotionIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, PromotionRef>();

        return await _db.Promotions.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new PromotionRef(p.Id, p.Name, p.Type.ToString()), ct);
    }
}

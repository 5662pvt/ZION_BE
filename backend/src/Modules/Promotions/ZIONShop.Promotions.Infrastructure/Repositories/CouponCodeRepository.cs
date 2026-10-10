using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class CouponCodeRepository : ICouponCodeRepository
{
    private readonly PromotionsDbContext _db;

    public CouponCodeRepository(PromotionsDbContext db) => _db = db;

    public Task<CouponCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.CouponCodes.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CouponCode?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        _db.CouponCodes.FirstOrDefaultAsync(c => c.Code == normalizedCode, cancellationToken);

    public Task<List<CouponCode>> GetByPromotionIdAsync(Guid promotionId, CancellationToken cancellationToken = default) =>
        _db.CouponCodes.Where(c => c.PromotionId == promotionId).OrderByDescending(c => c.CreatedAt).ToListAsync(cancellationToken);

    public async Task<HashSet<string>> GetExistingCodesAsync(IEnumerable<string> normalizedCodes, CancellationToken cancellationToken = default)
    {
        var codes = normalizedCodes.ToList();
        if (codes.Count == 0) return new HashSet<string>();

        var existing = await _db.CouponCodes.Where(c => codes.Contains(c.Code)).Select(c => c.Code).ToListAsync(cancellationToken);
        return new HashSet<string>(existing, StringComparer.Ordinal);
    }

    public Task<bool> ExistsByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default) =>
        _db.CouponCodes.AnyAsync(c => c.Code == normalizedCode, cancellationToken);

    public async Task AddAsync(CouponCode coupon, CancellationToken cancellationToken = default) =>
        await _db.CouponCodes.AddAsync(coupon, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<CouponCode> coupons, CancellationToken cancellationToken = default) =>
        await _db.CouponCodes.AddRangeAsync(coupons, cancellationToken);

    public void Update(CouponCode coupon) => _db.CouponCodes.Update(coupon);

    public IQueryable<CouponCode> Query() => _db.CouponCodes.AsQueryable();
}

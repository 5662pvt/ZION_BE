using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface ICouponCodeRepository
{
    Task<CouponCode?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CouponCode?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);
    Task<List<CouponCode>> GetByPromotionIdAsync(Guid promotionId, CancellationToken cancellationToken = default);
    Task<HashSet<string>> GetExistingCodesAsync(IEnumerable<string> normalizedCodes, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default);
    Task AddAsync(CouponCode coupon, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<CouponCode> coupons, CancellationToken cancellationToken = default);
    void Update(CouponCode coupon);
    IQueryable<CouponCode> Query();
}

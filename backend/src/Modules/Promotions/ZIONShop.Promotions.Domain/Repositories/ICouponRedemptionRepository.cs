using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface ICouponRedemptionRepository
{
    Task<CouponRedemption?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<int> CountActiveForUserAsync(Guid promotionId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<CouponRedemption>> GetStaleReservedAsync(DateTime olderThan, CancellationToken cancellationToken = default);
    Task AddAsync(CouponRedemption redemption, CancellationToken cancellationToken = default);
    void Update(CouponRedemption redemption);
}

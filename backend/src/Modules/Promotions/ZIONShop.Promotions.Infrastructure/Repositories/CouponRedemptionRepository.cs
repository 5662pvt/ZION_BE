using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class CouponRedemptionRepository : ICouponRedemptionRepository
{
    private readonly PromotionsDbContext _db;

    public CouponRedemptionRepository(PromotionsDbContext db) => _db = db;

    public Task<CouponRedemption?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.CouponRedemptions.FirstOrDefaultAsync(r => r.OrderId == orderId, cancellationToken);

    public Task<int> CountActiveForUserAsync(Guid promotionId, Guid userId, CancellationToken cancellationToken = default) =>
        _db.CouponRedemptions.CountAsync(
            r => r.PromotionId == promotionId && r.UserId == userId &&
                 (r.Status == RedemptionStatus.Reserved || r.Status == RedemptionStatus.Committed),
            cancellationToken);

    public Task<List<CouponRedemption>> GetStaleReservedAsync(DateTime olderThan, CancellationToken cancellationToken = default) =>
        _db.CouponRedemptions
            .Where(r => r.Status == RedemptionStatus.Reserved && r.CreatedAt < olderThan)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(CouponRedemption redemption, CancellationToken cancellationToken = default) =>
        await _db.CouponRedemptions.AddAsync(redemption, cancellationToken);

    public void Update(CouponRedemption redemption) => _db.CouponRedemptions.Update(redemption);
}

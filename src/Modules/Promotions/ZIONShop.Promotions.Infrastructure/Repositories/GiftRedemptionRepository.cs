using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class GiftRedemptionRepository : IGiftRedemptionRepository
{
    private readonly PromotionsDbContext _db;

    public GiftRedemptionRepository(PromotionsDbContext db) => _db = db;

    public Task<GiftRedemption?> GetByPromotionAndOrderAsync(Guid promotionId, Guid orderId, CancellationToken cancellationToken = default) =>
        _db.GiftRedemptions.FirstOrDefaultAsync(r => r.PromotionId == promotionId && r.OrderId == orderId, cancellationToken);

    public Task<List<GiftRedemption>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.GiftRedemptions.Where(r => r.OrderId == orderId).ToListAsync(cancellationToken);

    public Task<int> CountActiveForUserAsync(Guid promotionId, Guid userId, CancellationToken cancellationToken = default) =>
        _db.GiftRedemptions.CountAsync(
            r => r.PromotionId == promotionId && r.UserId == userId &&
                 (r.Status == RedemptionStatus.Reserved || r.Status == RedemptionStatus.Committed),
            cancellationToken);

    public Task<List<GiftRedemption>> GetStaleReservedAsync(DateTime olderThan, CancellationToken cancellationToken = default) =>
        _db.GiftRedemptions
            .Where(r => r.Status == RedemptionStatus.Reserved && r.CreatedAt < olderThan)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(GiftRedemption redemption, CancellationToken cancellationToken = default) =>
        await _db.GiftRedemptions.AddAsync(redemption, cancellationToken);

    public void Update(GiftRedemption redemption) => _db.GiftRedemptions.Update(redemption);
}

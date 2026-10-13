using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface IGiftRedemptionRepository
{
    Task<GiftRedemption?> GetByPromotionAndOrderAsync(Guid promotionId, Guid orderId, CancellationToken cancellationToken = default);
    Task<List<GiftRedemption>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<int> CountActiveForUserAsync(Guid promotionId, Guid userId, CancellationToken cancellationToken = default);
    Task<List<GiftRedemption>> GetStaleReservedAsync(DateTime olderThan, CancellationToken cancellationToken = default);
    Task AddAsync(GiftRedemption redemption, CancellationToken cancellationToken = default);
    void Update(GiftRedemption redemption);
}

using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Domain.Repositories;

public interface IFlashSaleRedemptionRepository
{
    Task<FlashSaleRedemption?> GetByItemAndOrderAsync(Guid flashSaleItemId, Guid orderId, CancellationToken cancellationToken = default);
    Task<List<FlashSaleRedemption>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<List<FlashSaleRedemption>> GetStaleReservedAsync(DateTime olderThan, CancellationToken cancellationToken = default);
    Task AddAsync(FlashSaleRedemption redemption, CancellationToken cancellationToken = default);
    void Update(FlashSaleRedemption redemption);
}

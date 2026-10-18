using Microsoft.EntityFrameworkCore;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.Promotions.Infrastructure.Persistence;

namespace ZIONShop.Promotions.Infrastructure.Repositories;

public class FlashSaleRedemptionRepository : IFlashSaleRedemptionRepository
{
    private readonly PromotionsDbContext _db;

    public FlashSaleRedemptionRepository(PromotionsDbContext db) => _db = db;

    public Task<FlashSaleRedemption?> GetByItemAndOrderAsync(Guid flashSaleItemId, Guid orderId, CancellationToken cancellationToken = default) =>
        _db.FlashSaleRedemptions.FirstOrDefaultAsync(r => r.FlashSaleItemId == flashSaleItemId && r.OrderId == orderId, cancellationToken);

    public Task<List<FlashSaleRedemption>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.FlashSaleRedemptions.Where(r => r.OrderId == orderId).ToListAsync(cancellationToken);

    public Task<List<FlashSaleRedemption>> GetStaleReservedAsync(DateTime olderThan, CancellationToken cancellationToken = default) =>
        _db.FlashSaleRedemptions
            .Where(r => r.Status == RedemptionStatus.Reserved && r.CreatedAt < olderThan)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(FlashSaleRedemption redemption, CancellationToken cancellationToken = default) =>
        await _db.FlashSaleRedemptions.AddAsync(redemption, cancellationToken);

    public void Update(FlashSaleRedemption redemption) => _db.FlashSaleRedemptions.Update(redemption);
}

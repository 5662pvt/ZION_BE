using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Services;

public class PurchaseVerifier : IPurchaseVerifier
{
    private readonly OrdersDbContext _db;

    public PurchaseVerifier(OrdersDbContext db) => _db = db;

    public async Task<Guid?> GetCompletedOrderIdAsync(Guid userId, Guid productId, CancellationToken ct = default)
    {
        var orderId = await _db.Orders.AsNoTracking()
            .Where(o => o.UserId == userId && o.Status == OrderStatus.Completed && o.Lines.Any(l => l.ProductId == productId))
            .OrderByDescending(o => o.CreatedDate)
            .Select(o => o.Id)
            .FirstOrDefaultAsync(ct);

        return orderId == Guid.Empty ? null : orderId;
    }
}

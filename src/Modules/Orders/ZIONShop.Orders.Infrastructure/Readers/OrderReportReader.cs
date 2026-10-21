using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Readers;

public class OrderReportReader : IOrderReportReader
{
    private readonly OrdersDbContext _db;

    public OrderReportReader(OrdersDbContext db) => _db = db;

    public async Task<IReadOnlyList<CompletedOrderRow>> GetCompletedOrdersAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var completions = await _db.OrderStatusHistories.AsNoTracking()
            .Where(h => h.Status == OrderStatus.Completed && h.At >= fromUtc && h.At < toUtc)
            .GroupBy(h => h.OrderId)
            .Select(g => new { OrderId = g.Key, CompletedAtUtc = g.Max(h => h.At) })
            .ToListAsync(ct);

        if (completions.Count == 0) return Array.Empty<CompletedOrderRow>();

        var orderIds = completions.Select(c => c.OrderId).ToList();
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => orderIds.Contains(o.Id) && o.Status != OrderStatus.Returned)
            .ToListAsync(ct);

        var completedAtByOrder = completions.ToDictionary(c => c.OrderId, c => c.CompletedAtUtc);

        return orders.Select(o => new CompletedOrderRow(
            o.Id,
            completedAtByOrder[o.Id],
            o.Subtotal,
            o.DiscountAmount,
            o.ShippingFee,
            o.PaymentMethod.ToString(),
            o.CouponCode,
            o.DiscountTarget,
            o.Lines.Select(l => new ReportOrderLine(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice, l.IsGift, l.GiftPromotionId)).ToList())
        ).ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> GetPlacedStatusCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var counts = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedDate >= fromUtc && o.CreatedDate < toUtc)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var result = Enum.GetNames<OrderStatus>().ToDictionary(name => name, _ => 0);
        foreach (var c in counts)
            result[c.Status.ToString()] = c.Count;

        return result;
    }

    public Task<int> CountAwaitingShipmentAsync(CancellationToken ct = default) =>
        _db.Orders.AsNoTracking().CountAsync(o => o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Paid, ct);
}

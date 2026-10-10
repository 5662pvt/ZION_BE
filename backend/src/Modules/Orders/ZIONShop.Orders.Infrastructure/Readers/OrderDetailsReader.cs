using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Readers;

public class OrderDetailsReader : IOrderDetailsReader
{
    private readonly OrdersDbContext _db;

    public OrderDetailsReader(OrdersDbContext db) => _db = db;

    public async Task<OrderDetails?> GetAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null) return null;

        var lines = order.Lines
            .Select(l => new OrderDetailsLine(l.ProductName, l.Quantity, l.UnitPrice, l.LineTotal))
            .ToList();

        return new OrderDetails(
            order.Id,
            order.UserId,
            order.Code,
            order.Status.ToString(),
            order.PaymentMethod.ToString(),
            order.Subtotal,
            order.DiscountAmount,
            order.CouponCode,
            order.ShippingFee,
            order.Total,
            order.Currency,
            order.ShippingAddress.RecipientName,
            order.ShippingAddress.PhoneNumber,
            $"{order.ShippingAddress.StreetAddress}, {order.ShippingAddress.Ward}, {order.ShippingAddress.Province}",
            order.CancelReason,
            order.CreatedDate,
            lines);
    }
}

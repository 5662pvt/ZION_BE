namespace ZIONShop.Contracts.Orders;

public sealed record ReportOrderLine(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, bool IsGift, Guid? GiftPromotionId);

public sealed record CompletedOrderRow(Guid OrderId, DateTime CompletedAtUtc, decimal Subtotal, decimal DiscountAmount, decimal ShippingFee,
    string PaymentMethod, string? CouponCode, string? DiscountTarget, IReadOnlyList<ReportOrderLine> Lines, decimal PointsDiscount = 0m);

public interface IOrderReportReader
{
    /// <summary>Orders whose OrderStatusHistory 'Completed' row At falls in [fromUtc, toUtc). AsNoTracking; lines included.</summary>
    Task<IReadOnlyList<CompletedOrderRow>> GetCompletedOrdersAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Orders placed (CreatedDate) in [fromUtc, toUtc), grouped by current Status. All OrderStatus names are present (0 when none).</summary>
    Task<IReadOnlyDictionary<string, int>> GetPlacedStatusCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>Current orders waiting for shipment (Status Confirmed or Paid).</summary>
    Task<int> CountAwaitingShipmentAsync(CancellationToken ct = default);
}

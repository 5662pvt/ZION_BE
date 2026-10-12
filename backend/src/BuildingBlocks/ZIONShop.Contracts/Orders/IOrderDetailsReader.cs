namespace ZIONShop.Contracts.Orders;

public sealed record OrderDetailsLine(string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal, bool IsGift = false);

public sealed record OrderDetails(Guid OrderId, Guid UserId, string Code, string Status, string PaymentMethod,
    decimal Subtotal, decimal DiscountAmount, string? CouponCode, string? DiscountTarget, decimal ShippingFee, decimal Total, string Currency,
    string RecipientName, string PhoneNumber, string AddressLine, string? CancelReason, DateTime CreatedAt, IReadOnlyList<OrderDetailsLine> Lines);

public interface IOrderDetailsReader
{
    Task<OrderDetails?> GetAsync(Guid orderId, CancellationToken ct = default);
}

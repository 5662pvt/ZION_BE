namespace ZIONShop.Orders.Application.DTOs;

public record OrderDto(
    Guid Id,
    string Code,
    string Status,
    string PaymentMethod,
    decimal Subtotal,
    decimal ShippingFee,
    string? CouponCode,
    decimal DiscountAmount,
    string? DiscountTarget,
    decimal Total,
    string Currency,
    DateTime CreatedAt,
    string? Note,
    string? CancelReason,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyList<OrderStatusHistoryDto> History,
    ReturnRequestDto? ReturnRequest = null,
    int PointsUsed = 0,
    decimal PointsDiscount = 0m);

public record OrderStatusHistoryDto(string Status, DateTime At, string? Note);

public record ShippingAddressDto(
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string Ward,
    string StreetAddress);

public record OrderLineDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal, bool IsGift, Guid VariantId, string? VariantLabel, string? Sku);

public record OrderSummaryDto(Guid Id, string Code, string Status, decimal Total, string Currency, DateTime CreatedAt, int ItemCount);

public record AdminOrderSummaryDto(
    Guid Id,
    string Code,
    string Status,
    string PaymentMethod,
    decimal Total,
    string Currency,
    DateTime CreatedAt,
    int ItemCount,
    string RecipientName,
    string PhoneNumber);

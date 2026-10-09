namespace ZIONShop.Orders.Application.DTOs;

public record OrderDto(
    Guid Id,
    string Code,
    string Status,
    string PaymentMethod,
    decimal Subtotal,
    decimal ShippingFee,
    decimal Total,
    string Currency,
    DateTime CreatedAt,
    string? Note,
    string? CancelReason,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderLineDto> Lines);

public record ShippingAddressDto(
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string Ward,
    string StreetAddress);

public record OrderLineDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderSummaryDto(Guid Id, string Code, string Status, decimal Total, string Currency, DateTime CreatedAt, int ItemCount);

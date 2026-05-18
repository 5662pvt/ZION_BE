namespace ZIONShop.Cart.Application.DTOs;

public record CartDto(
    Guid Id,
    Guid? UserId,
    string? AnonymousId,
    decimal Subtotal,
    string Currency,
    int TotalQuantity,
    IReadOnlyList<CartItemDto> Items);

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    string Currency);

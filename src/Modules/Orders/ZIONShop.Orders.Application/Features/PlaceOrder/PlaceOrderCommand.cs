using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.PlaceOrder;

public record PlaceOrderCommand(
    Guid UserId,
    Guid AddressId,
    string PaymentMethod,
    string? Note,
    string? CouponCode = null,
    string? IdempotencyKey = null) : IRequest<Result<PlaceOrderResult>>;

/// <summary>Wraps the placed/replayed order so the controller can set the <c>Idempotent-Replayed</c>
/// response header without changing the 200 envelope's <c>data</c> shape (still a plain <see cref="OrderDto"/>).</summary>
public record PlaceOrderResult(OrderDto Order, bool Replayed);

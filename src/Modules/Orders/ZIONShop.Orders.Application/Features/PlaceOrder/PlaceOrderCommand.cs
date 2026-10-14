using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.PlaceOrder;

public record PlaceOrderCommand(Guid UserId, Guid AddressId, string PaymentMethod, string? Note, string? CouponCode = null) : IRequest<Result<OrderDto>>;

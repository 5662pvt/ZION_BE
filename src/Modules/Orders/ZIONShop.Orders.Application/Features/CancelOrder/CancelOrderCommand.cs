using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.CancelOrder;

public record CancelOrderCommand(Guid OrderId, Guid RequestingUserId, bool IsStaffOrAdmin, string? Reason) : IRequest<Result<OrderDto>>;

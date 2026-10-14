using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetOrder;

public record GetOrderQuery(Guid OrderId, Guid RequestingUserId, bool IsStaffOrAdmin) : IRequest<Result<OrderDto>>;

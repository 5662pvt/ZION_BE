using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.CompleteOrder;

public record CompleteOrderCommand(Guid OrderId) : IRequest<Result<OrderDto>>;

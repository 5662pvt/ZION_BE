using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.ShipOrder;

public record ShipOrderCommand(Guid OrderId) : IRequest<Result<OrderDto>>;

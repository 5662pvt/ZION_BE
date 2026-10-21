using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.RequestReturn;

public record RequestReturnCommand(Guid OrderId, Guid UserId, string Reason, string? Note) : IRequest<Result<ReturnRequestDto>>;

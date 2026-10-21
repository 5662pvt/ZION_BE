using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetReturnById;

public record GetReturnByIdQuery(Guid ReturnRequestId) : IRequest<Result<AdminReturnRequestDto>>;

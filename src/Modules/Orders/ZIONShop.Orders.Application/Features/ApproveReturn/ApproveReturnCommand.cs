using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.ApproveReturn;

public record ApproveReturnCommand(Guid ReturnRequestId) : IRequest<Result<AdminReturnRequestDto>>;

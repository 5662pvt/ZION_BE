using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.RejectReturn;

public record RejectReturnCommand(Guid ReturnRequestId, string Reason) : IRequest<Result<AdminReturnRequestDto>>;

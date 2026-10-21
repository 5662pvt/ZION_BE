using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.MarkReceived;

public record MarkReceivedCommand(Guid ReturnRequestId) : IRequest<Result<AdminReturnRequestDto>>;

using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.MarkShippedBack;

public record MarkShippedBackCommand(Guid ReturnRequestId, Guid RequestingUserId, string? TrackingCode) : IRequest<Result<ReturnRequestDto>>;

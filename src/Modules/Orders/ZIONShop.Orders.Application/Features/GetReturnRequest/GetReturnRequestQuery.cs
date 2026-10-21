using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetReturnRequest;

public record GetReturnRequestQuery(Guid OrderId, Guid RequestingUserId, bool IsStaffOrAdmin) : IRequest<Result<ReturnRequestDto?>>;

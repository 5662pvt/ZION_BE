using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetReturnRequest;

/// <summary>Returns the order's latest return request, or a successful result carrying a null value
/// when the order has none yet -- only the order lookup itself (not-found / not-owner) fails.</summary>
public class GetReturnRequestQueryHandler : IRequestHandler<GetReturnRequestQuery, Result<ReturnRequestDto?>>
{
    private readonly IOrderRepository _orders;
    private readonly IReturnRequestRepository _returns;

    public GetReturnRequestQueryHandler(IOrderRepository orders, IReturnRequestRepository returns)
    {
        _orders = orders;
        _returns = returns;
    }

    public async Task<Result<ReturnRequestDto?>> Handle(GetReturnRequestQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return Result.Failure<ReturnRequestDto?>(OrdersErrors.OrderNotFound);
        if (!request.IsStaffOrAdmin && order.UserId != request.RequestingUserId)
            return Result.Failure<ReturnRequestDto?>(OrdersErrors.OrderNotFound);

        var latest = await _returns.GetLatestByOrderIdAsync(order.Id, cancellationToken);
        return Result.Success(latest?.ToDto(order.Code));
    }
}

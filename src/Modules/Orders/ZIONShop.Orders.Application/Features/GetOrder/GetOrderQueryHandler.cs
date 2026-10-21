using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetOrder;

public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IReturnRequestRepository _returns;

    public GetOrderQueryHandler(IOrderRepository orders, IReturnRequestRepository returns)
    {
        _orders = orders;
        _returns = returns;
    }

    public async Task<Result<OrderDto>> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return Result.Failure<OrderDto>(OrdersErrors.OrderNotFound);
        if (!request.IsStaffOrAdmin && order.UserId != request.RequestingUserId)
            return Result.Failure<OrderDto>(OrdersErrors.OrderNotFound);

        var latestReturn = await _returns.GetLatestByOrderIdAsync(order.Id, cancellationToken);
        var returnDto = latestReturn?.ToDto(order.Code);

        return Result.Success(order.ToDto(returnDto));
    }
}

using MediatR;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.CancelOrder;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;

    public CancelOrderCommandHandler(IOrderRepository orders, IOrdersUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task<Result<OrderDto>> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return Result.Failure<OrderDto>(OrdersErrors.OrderNotFound);
        if (!request.IsStaffOrAdmin && order.UserId != request.RequestingUserId)
            return Result.Failure<OrderDto>(OrdersErrors.OrderNotFound);

        var result = order.Cancel(request.Reason, request.IsStaffOrAdmin);
        if (result.IsFailure) return Result.Failure<OrderDto>(result.Error);

        _uow.AddOutboxEvent(new OrderCancelledIntegrationEvent(order.Id, order.UserId, order.CancelReason!));

        _orders.Update(order);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(order.ToDto());
    }
}

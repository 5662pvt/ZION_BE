using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.CompleteOrder;

public class CompleteOrderCommandHandler : IRequestHandler<CompleteOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;

    public CompleteOrderCommandHandler(IOrderRepository orders, IOrdersUnitOfWork uow)
    {
        _orders = orders;
        _uow = uow;
    }

    public async Task<Result<OrderDto>> Handle(CompleteOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null) return Result.Failure<OrderDto>(OrdersErrors.OrderNotFound);

        var result = order.Complete();
        if (result.IsFailure) return Result.Failure<OrderDto>(result.Error);

        _orders.Update(order);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(order.ToDto());
    }
}

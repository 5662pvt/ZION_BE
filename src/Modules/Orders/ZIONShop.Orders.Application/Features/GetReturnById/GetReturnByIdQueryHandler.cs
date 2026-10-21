using MediatR;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetReturnById;

public class GetReturnByIdQueryHandler : IRequestHandler<GetReturnByIdQuery, Result<AdminReturnRequestDto>>
{
    private readonly IReturnRequestRepository _returns;
    private readonly IOrderRepository _orders;

    public GetReturnByIdQueryHandler(IReturnRequestRepository returns, IOrderRepository orders)
    {
        _returns = returns;
        _orders = orders;
    }

    public async Task<Result<AdminReturnRequestDto>> Handle(GetReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var returnRequest = await _returns.GetByIdAsync(request.ReturnRequestId, cancellationToken);
        if (returnRequest is null) return Result.Failure<AdminReturnRequestDto>(OrdersErrors.ReturnRequestNotFound);

        var order = await _orders.GetByIdAsync(returnRequest.OrderId, cancellationToken);
        if (order is null) return Result.Failure<AdminReturnRequestDto>(OrdersErrors.OrderNotFound);

        return Result.Success(returnRequest.ToAdminDto(order.Code, order.ShippingAddress.RecipientName, order.ShippingAddress.PhoneNumber));
    }
}

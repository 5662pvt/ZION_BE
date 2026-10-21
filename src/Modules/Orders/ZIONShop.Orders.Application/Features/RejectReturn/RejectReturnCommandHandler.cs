using MediatR;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.RejectReturn;

public class RejectReturnCommandHandler : IRequestHandler<RejectReturnCommand, Result<AdminReturnRequestDto>>
{
    private readonly IReturnRequestRepository _returns;
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public RejectReturnCommandHandler(IReturnRequestRepository returns, IOrderRepository orders, IOrdersUnitOfWork uow, TimeProvider timeProvider)
    {
        _returns = returns;
        _orders = orders;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminReturnRequestDto>> Handle(RejectReturnCommand request, CancellationToken cancellationToken)
    {
        var returnRequest = await _returns.GetByIdAsync(request.ReturnRequestId, cancellationToken);
        if (returnRequest is null) return Result.Failure<AdminReturnRequestDto>(OrdersErrors.ReturnRequestNotFound);

        var result = returnRequest.Reject(request.Reason, _timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure) return Result.Failure<AdminReturnRequestDto>(result.Error);

        var order = await _orders.GetByIdAsync(returnRequest.OrderId, cancellationToken);
        if (order is null) return Result.Failure<AdminReturnRequestDto>(OrdersErrors.OrderNotFound);

        _uow.AddOutboxEvent(new OrderReturnDecidedIntegrationEvent(order.Id, returnRequest.UserId, returnRequest.Id, Approved: false, returnRequest.RejectReason));

        _returns.Update(returnRequest);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(returnRequest.ToAdminDto(order.Code, order.ShippingAddress.RecipientName, order.ShippingAddress.PhoneNumber));
    }
}

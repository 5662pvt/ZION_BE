using MediatR;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.MarkShippedBack;

public class MarkShippedBackCommandHandler : IRequestHandler<MarkShippedBackCommand, Result<ReturnRequestDto>>
{
    private readonly IReturnRequestRepository _returns;
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public MarkShippedBackCommandHandler(IReturnRequestRepository returns, IOrderRepository orders, IOrdersUnitOfWork uow, TimeProvider timeProvider)
    {
        _returns = returns;
        _orders = orders;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ReturnRequestDto>> Handle(MarkShippedBackCommand request, CancellationToken cancellationToken)
    {
        var returnRequest = await _returns.GetByIdAsync(request.ReturnRequestId, cancellationToken);
        if (returnRequest is null || returnRequest.UserId != request.RequestingUserId)
            return Result.Failure<ReturnRequestDto>(OrdersErrors.ReturnRequestNotFound);

        var result = returnRequest.MarkShippedBack(request.TrackingCode, _timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure) return Result.Failure<ReturnRequestDto>(result.Error);

        var order = await _orders.GetByIdAsync(returnRequest.OrderId, cancellationToken);
        if (order is null) return Result.Failure<ReturnRequestDto>(OrdersErrors.OrderNotFound);

        _uow.AddOutboxEvent(new OrderReturnShippedBackIntegrationEvent(order.Id, returnRequest.UserId, returnRequest.Id, returnRequest.ReturnTrackingCode));

        _returns.Update(returnRequest);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(returnRequest.ToDto(order.Code));
    }
}

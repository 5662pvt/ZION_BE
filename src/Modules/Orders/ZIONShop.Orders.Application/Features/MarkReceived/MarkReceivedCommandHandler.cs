using MediatR;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.MarkReceived;

public class MarkReceivedCommandHandler : IRequestHandler<MarkReceivedCommand, Result<AdminReturnRequestDto>>
{
    private readonly IReturnRequestRepository _returns;
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public MarkReceivedCommandHandler(IReturnRequestRepository returns, IOrderRepository orders, IOrdersUnitOfWork uow, TimeProvider timeProvider)
    {
        _returns = returns;
        _orders = orders;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<Result<AdminReturnRequestDto>> Handle(MarkReceivedCommand request, CancellationToken cancellationToken)
    {
        var returnRequest = await _returns.GetByIdAsync(request.ReturnRequestId, cancellationToken);
        if (returnRequest is null) return Result.Failure<AdminReturnRequestDto>(OrdersErrors.ReturnRequestNotFound);

        var result = returnRequest.MarkReceived(_timeProvider.GetUtcNow().UtcDateTime);
        if (result.IsFailure) return Result.Failure<AdminReturnRequestDto>(result.Error);

        var order = await _orders.GetByIdAsync(returnRequest.OrderId, cancellationToken);
        if (order is null) return Result.Failure<AdminReturnRequestDto>(OrdersErrors.OrderNotFound);

        var lines = order.Lines.Select(l => new ReturnLine(l.ProductId, l.Quantity, l.VariantId)).ToList();
        _uow.AddOutboxEvent(new OrderReturnReceivedIntegrationEvent(
            order.Id, returnRequest.UserId, returnRequest.Id, returnRequest.RefundAmount, order.PaymentMethod.ToString(), lines));

        _returns.Update(returnRequest);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(returnRequest.ToAdminDto(order.Code, order.ShippingAddress.RecipientName, order.ShippingAddress.PhoneNumber));
    }
}

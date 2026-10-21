using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Exceptions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.RequestReturn;

public class RequestReturnCommandHandler : IRequestHandler<RequestReturnCommand, Result<ReturnRequestDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IReturnRequestRepository _returns;
    private readonly IOrdersUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ReturnsOptions _options;

    public RequestReturnCommandHandler(
        IOrderRepository orders,
        IReturnRequestRepository returns,
        IOrdersUnitOfWork uow,
        TimeProvider timeProvider,
        IOptions<ReturnsOptions> options)
    {
        _orders = orders;
        _returns = returns;
        _uow = uow;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async Task<Result<ReturnRequestDto>> Handle(RequestReturnCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ReturnReason>(request.Reason, ignoreCase: true, out var reason))
            return Result.Failure<ReturnRequestDto>(OrdersErrors.InvalidReturnReason);

        var order = await _orders.GetByIdAsync(request.OrderId, cancellationToken);
        if (order is null || order.UserId != request.UserId)
            return Result.Failure<ReturnRequestDto>(OrdersErrors.OrderNotFound);

        if (order.Status != OrderStatus.Completed)
            return Result.Failure<ReturnRequestDto>(OrdersErrors.ReturnNotEligible);

        var completedAt = order.StatusHistory
            .Where(h => h.Status == OrderStatus.Completed)
            .Select(h => h.At)
            .DefaultIfEmpty()
            .Max();
        if (completedAt == default)
            return Result.Failure<ReturnRequestDto>(OrdersErrors.ReturnNotEligible);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (now > completedAt.AddDays(_options.WindowDays))
            return Result.Failure<ReturnRequestDto>(OrdersErrors.ReturnWindowExpired);

        var active = await _returns.GetActiveByOrderIdAsync(order.Id, cancellationToken);
        if (active is not null)
            return Result.Failure<ReturnRequestDto>(OrdersErrors.ReturnAlreadyRequested);

        var createResult = ReturnRequest.Create(order.Id, request.UserId, reason, request.Note, order.Total, now);
        if (createResult.IsFailure) return Result.Failure<ReturnRequestDto>(createResult.Error);

        var returnRequest = createResult.Value;

        try
        {
            await _returns.AddAsync(returnRequest, cancellationToken);
            _uow.AddOutboxEvent(new OrderReturnRequestedIntegrationEvent(order.Id, order.UserId, returnRequest.Id, reason.ToString()));
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateReturnRequestException)
        {
            return Result.Failure<ReturnRequestDto>(OrdersErrors.ReturnAlreadyRequested);
        }

        return Result.Success(returnRequest.ToDto(order.Code));
    }
}

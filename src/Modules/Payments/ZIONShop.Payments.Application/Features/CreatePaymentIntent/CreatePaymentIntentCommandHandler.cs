using MediatR;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Application.Mappings;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.CreatePaymentIntent;

public class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, Result<PaymentIntentDto>>
{
    private readonly IPaymentRepository _payments;
    private readonly IPayableOrderRepository _payableOrders;
    private readonly IPaymentsUnitOfWork _uow;
    private readonly IPaymentGateway _gateway;
    private readonly TimeProvider _timeProvider;

    public CreatePaymentIntentCommandHandler(
        IPaymentRepository payments,
        IPayableOrderRepository payableOrders,
        IPaymentsUnitOfWork uow,
        IPaymentGateway gateway,
        TimeProvider timeProvider)
    {
        _payments = payments;
        _payableOrders = payableOrders;
        _uow = uow;
        _gateway = gateway;
        _timeProvider = timeProvider;
    }

    private static readonly string[] ValidChannels = { "Card", "EWallet", "BankTransfer" };

    // Channel validation happens here rather than via FluentValidation: ValidationBehavior always prefixes
    // a FluentValidation failure code with the property name, which cannot produce the contract-exact
    // "Payments.InvalidChannel" code (same reasoning as PlaceOrderCommandValidator).
    private static string? NormalizeChannel(string? input)
    {
        var value = string.IsNullOrWhiteSpace(input) ? "Card" : input.Trim();
        return ValidChannels.FirstOrDefault(c => string.Equals(c, value, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<Result<PaymentIntentDto>> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {
        var channel = NormalizeChannel(request.Channel);
        if (channel is null) return Result.Failure<PaymentIntentDto>(PaymentsErrors.InvalidChannel);

        var existingByKey = await _payments.GetByIdempotencyKeyAsync(request.UserId, request.IdempotencyKey, cancellationToken);
        if (existingByKey is not null) return Result.Success(existingByKey.ToDto(_gateway));

        var payableOrder = await _payableOrders.GetByOrderIdAsync(request.OrderId, cancellationToken);
        if (payableOrder is null)
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.OrderNotPayable);
        if (payableOrder.UserId != request.UserId)
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.OrderNotFound);

        var stateError = payableOrder.State switch
        {
            PayableOrderState.AwaitingStock => PaymentsErrors.OrderNotPayable,
            PayableOrderState.Cancelled => PaymentsErrors.OrderCancelled,
            PayableOrderState.Paid => PaymentsErrors.OrderAlreadyPaid,
            _ => null
        };
        if (stateError is not null) return Result.Failure<PaymentIntentDto>(stateError);

        var existingPending = await _payments.GetPendingByOrderIdAsync(request.OrderId, cancellationToken);
        if (existingPending is not null) return Result.Success(existingPending.ToDto(_gateway));

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var payment = Payment.Create(request.OrderId, request.UserId, payableOrder.Amount, payableOrder.Currency, "Fake", request.IdempotencyKey, now, channel);
        var (intentId, _) = await _gateway.CreateIntentAsync(payment, cancellationToken);
        payment.AssignIntent(intentId);

        await _payments.AddAsync(payment, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(payment.ToDto(_gateway));
    }
}

using System.Text.Json;
using ZIONShop.Contracts.Payments;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Application.Mappings;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Services;

/// <summary>
/// Single code path for applying a fake-gateway webhook delivery, shared by the real webhook endpoint
/// (signature read from the request) and the dev-only simulate endpoint (signs its own payload and runs
/// it through this exact same verify-then-apply pipeline). Idempotent, keyed on <see cref="FakeWebhookPayload.EventId"/>.
/// </summary>
public class FakeWebhookProcessor
{
    private readonly IPaymentRepository _payments;
    private readonly IPayableOrderRepository _payableOrders;
    private readonly IProcessedWebhookEventRepository _processedEvents;
    private readonly IPaymentsUnitOfWork _uow;
    private readonly IPaymentGateway _gateway;
    private readonly TimeProvider _timeProvider;

    public FakeWebhookProcessor(
        IPaymentRepository payments,
        IPayableOrderRepository payableOrders,
        IProcessedWebhookEventRepository processedEvents,
        IPaymentsUnitOfWork uow,
        IPaymentGateway gateway,
        TimeProvider timeProvider)
    {
        _payments = payments;
        _payableOrders = payableOrders;
        _processedEvents = processedEvents;
        _uow = uow;
        _gateway = gateway;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PaymentIntentDto>> ProcessAsync(string rawBody, string signature, CancellationToken cancellationToken)
    {
        if (!_gateway.VerifySignature(rawBody, signature))
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.InvalidSignature);

        FakeWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<FakeWebhookPayload>(rawBody, FakeWebhookJson.Options);
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.IntentId))
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.InvalidWebhookPayload);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (await _processedEvents.ExistsAsync(payload.EventId, cancellationToken))
        {
            var replayed = await _payments.GetByIntentIdAsync(payload.IntentId, cancellationToken);
            return replayed is null
                ? Result.Failure<PaymentIntentDto>(PaymentsErrors.PaymentNotFound)
                : Result.Success(replayed.ToDto(_gateway));
        }

        var payment = await _payments.GetByIntentIdAsync(payload.IntentId, cancellationToken);
        if (payment is null) return Result.Failure<PaymentIntentDto>(PaymentsErrors.PaymentNotFound);

        var payableOrder = await _payableOrders.GetByOrderIdAsync(payment.OrderId, cancellationToken);
        if (payableOrder is null || payableOrder.State == PayableOrderState.Cancelled || payment.Status != Domain.Enums.PaymentStatus.Pending)
        {
            await _processedEvents.AddAsync(payload.EventId, now, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.PaymentNotPending);
        }

        if (string.Equals(payload.Result, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            payment.Succeed(now);
            payableOrder.MarkPaid(now);
            _uow.AddOutboxEvent(new PaymentCompletedIntegrationEvent(payment.OrderId, payment.Id, payment.Amount, "Fake"));
        }
        else if (string.Equals(payload.Result, "failed", StringComparison.OrdinalIgnoreCase))
        {
            var reason = string.IsNullOrWhiteSpace(payload.Reason) ? "Thanh toán thất bại" : payload.Reason;
            payment.Fail(reason);
            _uow.AddOutboxEvent(new PaymentFailedIntegrationEvent(payment.OrderId, payment.Id, payment.FailureReason!));
        }
        else
        {
            await _processedEvents.AddAsync(payload.EventId, now, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Failure<PaymentIntentDto>(PaymentsErrors.InvalidWebhookPayload);
        }

        await _processedEvents.AddAsync(payload.EventId, now, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(payment.ToDto(_gateway));
    }
}

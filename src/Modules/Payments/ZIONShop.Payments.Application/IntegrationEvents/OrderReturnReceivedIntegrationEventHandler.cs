using Microsoft.Extensions.Logging;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Repositories;

namespace ZIONShop.Payments.Application.IntegrationEvents;

/// <summary>
/// Reacts to a whole-order return reaching Received by refunding it (simulated, no real gateway).
/// Online orders refund against their Succeeded <see cref="Payment"/> through <see cref="IPaymentGateway"/>;
/// COD orders never had a Payment row (see <see cref="OrderCreatedIntegrationEventHandler"/>) so the Refund
/// completes immediately as simulated cash-back. One <see cref="Refund"/> per ReturnRequestId (unique index
/// backs this beyond the inbox's own EventId dedupe, since redelivery after a crash between SaveChanges calls
/// is otherwise possible).
/// </summary>
public class OrderReturnReceivedIntegrationEventHandler : IIntegrationEventHandler<OrderReturnReceivedIntegrationEvent>
{
    private readonly IRefundRepository _refunds;
    private readonly IPaymentRepository _payments;
    private readonly IPaymentGateway _gateway;
    private readonly IPaymentsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderReturnReceivedIntegrationEventHandler> _logger;

    public OrderReturnReceivedIntegrationEventHandler(
        IRefundRepository refunds,
        IPaymentRepository payments,
        IPaymentGateway gateway,
        IPaymentsUnitOfWork uow,
        TimeProvider timeProvider,
        ILogger<OrderReturnReceivedIntegrationEventHandler> logger)
    {
        _refunds = refunds;
        _payments = payments;
        _gateway = gateway;
        _uow = uow;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task HandleAsync(OrderReturnReceivedIntegrationEvent @event, CancellationToken cancellationToken = default)
    {
        if (await _refunds.GetByReturnRequestIdAsync(@event.ReturnRequestId, cancellationToken) is not null)
            return; // idempotent beyond the inbox: one refund per return request

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (string.Equals(@event.PaymentMethod, "COD", StringComparison.OrdinalIgnoreCase))
        {
            var refund = Refund.Create(@event.OrderId, @event.UserId, @event.ReturnRequestId, paymentId: null, @event.RefundAmount, "VND", "COD", now);
            refund.MarkSucceeded("cod_" + Guid.NewGuid().ToString("N"), now);

            await _refunds.AddAsync(refund, cancellationToken);
            _uow.AddOutboxEvent(new OrderRefundedIntegrationEvent(@event.OrderId, @event.UserId, @event.ReturnRequestId, @event.RefundAmount, "COD"));
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        var payment = await _payments.GetSucceededByOrderIdAsync(@event.OrderId, cancellationToken);
        if (payment is null)
        {
            _logger.LogWarning(
                "No succeeded Payment found for order {OrderId} on Online refund for return request {ReturnRequestId}; recording Refund as Failed.",
                @event.OrderId, @event.ReturnRequestId);

            var failedRefund = Refund.Create(@event.OrderId, @event.UserId, @event.ReturnRequestId, paymentId: null, @event.RefundAmount, "VND", "Online", now);
            failedRefund.MarkFailed("Succeeded payment not found for this order", now);

            await _refunds.AddAsync(failedRefund, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
            return;
        }

        var onlineRefund = Refund.Create(@event.OrderId, @event.UserId, @event.ReturnRequestId, payment.Id, @event.RefundAmount, "VND", "Online", now);
        var (succeeded, providerRefundId, failureReason) = await _gateway.RefundAsync(payment, @event.RefundAmount, cancellationToken);

        if (succeeded)
        {
            onlineRefund.MarkSucceeded(providerRefundId!, now);
            payment.MarkRefunded(now);
            await _refunds.AddAsync(onlineRefund, cancellationToken);
            _uow.AddOutboxEvent(new OrderRefundedIntegrationEvent(@event.OrderId, @event.UserId, @event.ReturnRequestId, @event.RefundAmount, "Online"));
        }
        else
        {
            onlineRefund.MarkFailed(failureReason ?? "Refund failed", now);
            await _refunds.AddAsync(onlineRefund, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}

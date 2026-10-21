using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Domain.Entities;

public class Refund : AggregateRoot
{
    private Refund() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ReturnRequestId { get; private set; }
    public Guid? PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string Method { get; private set; } = string.Empty;
    public RefundStatus Status { get; private set; }
    public string? ProviderRefundId { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public static Refund Create(Guid orderId, Guid userId, Guid returnRequestId, Guid? paymentId, decimal amount, string currency, string method, DateTime now)
    {
        return new Refund
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            UserId = userId,
            ReturnRequestId = returnRequestId,
            PaymentId = paymentId,
            Amount = amount,
            Currency = currency,
            Method = method,
            Status = RefundStatus.Pending,
            CreatedDate = now
        };
    }

    public Result MarkSucceeded(string providerRefundId, DateTime now)
    {
        if (Status != RefundStatus.Pending) return Result.Failure(PaymentsErrors.RefundNotPending);
        Status = RefundStatus.Succeeded;
        ProviderRefundId = providerRefundId;
        CompletedAt = now;
        return Result.Success();
    }

    public Result MarkFailed(string reason, DateTime now)
    {
        if (Status != RefundStatus.Pending) return Result.Failure(PaymentsErrors.RefundNotPending);
        Status = RefundStatus.Failed;
        FailureReason = reason;
        CompletedAt = now;
        return Result.Success();
    }
}

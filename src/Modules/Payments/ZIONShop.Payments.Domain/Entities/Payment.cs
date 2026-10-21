using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Domain.Entities;

public class Payment : AggregateRoot
{
    private Payment() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string Provider { get; private set; } = string.Empty;
    public string IntentId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string Channel { get; private set; } = "Card";

    public static Payment Create(Guid orderId, Guid userId, decimal amount, string currency, string provider, string idempotencyKey, DateTime now, string channel = "Card")
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            UserId = userId,
            Amount = amount,
            Currency = currency,
            Provider = provider,
            IdempotencyKey = idempotencyKey,
            Status = PaymentStatus.Pending,
            ExpiresAt = now.AddMinutes(15),
            Channel = channel
        };
    }

    /// <summary>Set once, right after the gateway generates the intent — never reassigned.</summary>
    public void AssignIntent(string intentId)
    {
        if (!string.IsNullOrEmpty(IntentId)) throw new InvalidOperationException("Intent already assigned to this payment.");
        IntentId = intentId;
    }

    public Result Succeed(DateTime now)
    {
        if (Status != PaymentStatus.Pending) return Result.Failure(PaymentsErrors.PaymentNotPending);
        Status = PaymentStatus.Succeeded;
        CompletedAt = now;
        return Result.Success();
    }

    public Result Fail(string reason)
    {
        if (Status != PaymentStatus.Pending) return Result.Failure(PaymentsErrors.PaymentNotPending);
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status != PaymentStatus.Pending) return Result.Failure(PaymentsErrors.PaymentNotPending);
        Status = PaymentStatus.Cancelled;
        return Result.Success();
    }

    public Result MarkRefunded(DateTime now)
    {
        if (Status != PaymentStatus.Succeeded) return Result.Failure(PaymentsErrors.PaymentNotRefundable);
        Status = PaymentStatus.Refunded;
        CompletedAt = now;
        return Result.Success();
    }
}

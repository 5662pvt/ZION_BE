using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Exceptions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Domain.Entities;

/// <summary>
/// Payments-owned read model of an order's payability, kept in sync via OrderCreated/InventoryReserved/
/// OrderCancelled/InventoryReservationExpired/InventoryReservationFailed integration events. Keyed by OrderId
/// (not a Payments-generated Id) since it is a 1:1 projection of an Orders-module aggregate.
/// </summary>
public class PayableOrder
{
    private PayableOrder() { }

    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public PayableOrderState State { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static PayableOrder Create(Guid orderId, Guid userId, decimal amount, string currency, DateTime now)
    {
        return new PayableOrder
        {
            OrderId = orderId,
            UserId = userId,
            Amount = amount,
            Currency = currency,
            State = PayableOrderState.AwaitingStock,
            UpdatedAt = now
        };
    }

    public Result MarkPayable(DateTime now)
    {
        if (State != PayableOrderState.AwaitingStock) return Result.Failure(PaymentsErrors.InvalidProjectionTransition);
        State = PayableOrderState.Payable;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkPaid(DateTime now)
    {
        if (State != PayableOrderState.Payable) return Result.Failure(PaymentsErrors.InvalidProjectionTransition);
        State = PayableOrderState.Paid;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkCancelled(DateTime now)
    {
        if (State is PayableOrderState.Cancelled or PayableOrderState.Paid) return Result.Failure(PaymentsErrors.InvalidProjectionTransition);
        State = PayableOrderState.Cancelled;
        UpdatedAt = now;
        return Result.Success();
    }
}

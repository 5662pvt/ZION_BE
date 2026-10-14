using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Repositories;

namespace ZIONShop.Payments.Application.IntegrationEvents;

/// <summary>
/// Shared by the three events that end a Payments projection's life (OrderCancelled,
/// InventoryReservationExpired, InventoryReservationFailed): marks the projection Cancelled and cancels
/// any still-Pending payment for that order, since the order can no longer be paid for.
/// </summary>
internal static class CancelPayableOrderHelper
{
    public static async Task<bool> CancelAsync(
        Guid orderId,
        IPayableOrderRepository payableOrders,
        IPaymentRepository payments,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var changed = false;

        var payableOrder = await payableOrders.GetByOrderIdAsync(orderId, cancellationToken);
        if (payableOrder is not null && payableOrder.MarkCancelled(now).IsSuccess)
            changed = true;

        var pendingPayment = await payments.GetPendingByOrderIdAsync(orderId, cancellationToken);
        if (pendingPayment is not null && pendingPayment.Status == PaymentStatus.Pending && pendingPayment.Cancel().IsSuccess)
            changed = true;

        return changed;
    }
}

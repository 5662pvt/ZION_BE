using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Application.Mappings;

public static class RefundMappingExtensions
{
    public static RefundDto ToDto(this Refund refund) => new(
        refund.Id,
        refund.OrderId,
        refund.ReturnRequestId,
        refund.PaymentId,
        refund.Amount,
        refund.Currency,
        refund.Method,
        refund.Status.ToString(),
        refund.ProviderRefundId,
        refund.FailureReason,
        refund.CreatedDate,
        refund.CompletedAt);
}

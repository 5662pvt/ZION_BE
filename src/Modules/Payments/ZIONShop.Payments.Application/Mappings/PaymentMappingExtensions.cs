using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Application.Mappings;

public static class PaymentMappingExtensions
{
    public static PaymentIntentDto ToDto(this Payment payment, IPaymentGateway gateway) => new(
        payment.Id,
        payment.IntentId,
        payment.OrderId,
        payment.Status.ToString(),
        payment.Amount,
        payment.Currency,
        gateway.BuildRedirectUrl(payment.IntentId),
        payment.ExpiresAt);
}

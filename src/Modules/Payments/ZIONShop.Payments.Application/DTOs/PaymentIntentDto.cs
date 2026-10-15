namespace ZIONShop.Payments.Application.DTOs;

public record PaymentIntentDto(
    Guid PaymentId,
    string IntentId,
    Guid OrderId,
    string Status,
    decimal Amount,
    string Currency,
    string RedirectUrl,
    DateTime ExpiresAt,
    string Channel);

namespace ZIONShop.Payments.Application.DTOs;

public record RefundDto(
    Guid Id,
    Guid OrderId,
    Guid ReturnRequestId,
    Guid? PaymentId,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? ProviderRefundId,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? CompletedAt);

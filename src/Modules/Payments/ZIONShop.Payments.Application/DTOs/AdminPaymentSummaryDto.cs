namespace ZIONShop.Payments.Application.DTOs;

public record AdminPaymentSummaryDto(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Provider,
    string Channel,
    string Status,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    DateTime ExpiresAt);

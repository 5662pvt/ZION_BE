namespace ZIONShop.Notifications.Application.DTOs;

public record NotificationLogDto(
    Guid Id,
    Guid EventId,
    string Type,
    string Channel,
    Guid UserId,
    Guid OrderId,
    string Recipient,
    string Subject,
    string Status,
    string? Error,
    DateTime CreatedAt,
    DateTime? SentAt);

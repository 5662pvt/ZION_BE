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

public record MyNotificationDto(
    Guid Id,
    string Type,
    string Subject,
    Guid OrderId,
    string Status,
    DateTime CreatedAt,
    DateTime? SentAt);

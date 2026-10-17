namespace ZIONShop.Notifications.Application.DTOs;

public record UserNotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    Guid? OrderId,
    bool IsRead,
    DateTime CreatedAt);

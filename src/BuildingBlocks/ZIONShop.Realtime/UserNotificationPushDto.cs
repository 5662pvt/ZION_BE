namespace ZIONShop.Realtime;

public sealed record UserNotificationPushDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    Guid? OrderId,
    DateTimeOffset CreatedAt,
    bool IsRead = false);

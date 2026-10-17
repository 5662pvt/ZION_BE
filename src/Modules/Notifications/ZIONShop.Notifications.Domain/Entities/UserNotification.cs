using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Notifications.Domain.Entities;

public class UserNotification : BaseEntity
{
    private UserNotification() { }

    public Guid UserId { get; private set; }
    public UserNotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public Guid? OrderId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public static UserNotification Create(Guid userId, UserNotificationType type, string title, string message, Guid? orderId) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            OrderId = orderId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
}

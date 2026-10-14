using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Notifications.Domain.Entities;

public class NotificationLog : BaseEntity
{
    private NotificationLog() { }

    public Guid EventId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Channel { get; private set; } = "Email";
    public Guid UserId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public string? Error { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }

    public static NotificationLog Sent(Guid eventId, NotificationType type, Guid userId, Guid orderId, string recipient, string subject, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Type = type,
            UserId = userId,
            OrderId = orderId,
            Recipient = recipient,
            Subject = subject,
            Status = NotificationStatus.Sent,
            CreatedAt = now,
            SentAt = now
        };

    public static NotificationLog Failed(Guid eventId, NotificationType type, Guid userId, Guid orderId, string recipient, string subject, string error, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Type = type,
            UserId = userId,
            OrderId = orderId,
            Recipient = recipient,
            Subject = subject,
            Status = NotificationStatus.Failed,
            Error = error,
            CreatedAt = now
        };

    public static NotificationLog Skipped(Guid eventId, NotificationType type, Guid userId, Guid orderId, string reason, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Type = type,
            UserId = userId,
            OrderId = orderId,
            Status = NotificationStatus.Skipped,
            Error = reason,
            CreatedAt = now
        };
}

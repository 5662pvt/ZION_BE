using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Application.Mappings;

public static class NotificationLogMappingExtensions
{
    public static NotificationLogDto ToDto(this NotificationLog log) => new(
        log.Id,
        log.EventId,
        log.Type.ToString(),
        log.Channel,
        log.UserId,
        log.OrderId,
        log.Recipient,
        log.Subject,
        log.Status.ToString(),
        log.Error,
        log.CreatedAt,
        log.SentAt);
}

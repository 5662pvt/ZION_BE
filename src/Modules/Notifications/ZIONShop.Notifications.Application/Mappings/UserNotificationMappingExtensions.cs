using ZIONShop.Notifications.Application.DTOs;
using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Application.Mappings;

public static class UserNotificationMappingExtensions
{
    public static UserNotificationDto ToDto(this UserNotification notification) => new(
        notification.Id,
        notification.Type.ToString(),
        notification.Title,
        notification.Message,
        notification.OrderId,
        notification.IsRead,
        notification.CreatedAt);
}

using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;

namespace ZIONShop.Notifications.Domain.Repositories;

public interface INotificationLogRepository
{
    Task<bool> ExistsAsync(Guid eventId, NotificationType type, CancellationToken cancellationToken = default);
    Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default);
    IQueryable<NotificationLog> Query();
}

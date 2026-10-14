using Microsoft.EntityFrameworkCore;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.Notifications.Infrastructure.Persistence;

namespace ZIONShop.Notifications.Infrastructure.Repositories;

public class NotificationLogRepository : INotificationLogRepository
{
    private readonly NotificationsDbContext _db;

    public NotificationLogRepository(NotificationsDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid eventId, NotificationType type, CancellationToken cancellationToken = default) =>
        _db.NotificationLogs.AnyAsync(l => l.EventId == eventId && l.Type == type, cancellationToken);

    public async Task AddAsync(NotificationLog log, CancellationToken cancellationToken = default) =>
        await _db.NotificationLogs.AddAsync(log, cancellationToken);

    public IQueryable<NotificationLog> Query() => _db.NotificationLogs.AsQueryable();
}

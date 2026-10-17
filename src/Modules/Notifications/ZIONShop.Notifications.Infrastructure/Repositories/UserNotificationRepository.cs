using Microsoft.EntityFrameworkCore;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Repositories;
using ZIONShop.Notifications.Infrastructure.Persistence;

namespace ZIONShop.Notifications.Infrastructure.Repositories;

public class UserNotificationRepository : IUserNotificationRepository
{
    private readonly NotificationsDbContext _db;

    public UserNotificationRepository(NotificationsDbContext db) => _db = db;

    public async Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default) =>
        await _db.UserNotifications.AddAsync(notification, cancellationToken);

    public IQueryable<UserNotification> QueryForUser(Guid userId) =>
        _db.UserNotifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt);

    public async Task<(IReadOnlyList<UserNotification> Items, int Total)> GetFeedPageAsync(
        Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.UserNotifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.UserNotifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public Task<UserNotification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        _db.UserNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);

    public Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.UserNotifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow), cancellationToken);
}

using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Repositories;

namespace ZIONShop.Notifications.Tests.Fakes;

public class FakeUserNotificationRepository : IUserNotificationRepository
{
    private readonly List<UserNotification> _items = new();

    public FakeUserNotificationRepository With(UserNotification notification)
    {
        _items.Add(notification);
        return this;
    }

    public Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default)
    {
        _items.Add(notification);
        return Task.CompletedTask;
    }

    public IQueryable<UserNotification> QueryForUser(Guid userId) =>
        _items.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).AsQueryable();

    public Task<(IReadOnlyList<UserNotification> Items, int Total)> GetFeedPageAsync(
        Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var query = _items.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        var filtered = query.ToList();
        var items = filtered
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<UserNotification> Items, int Total)>((items, filtered.Count));
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.Count(n => n.UserId == userId && !n.IsRead));

    public Task<UserNotification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(n => n.Id == id && n.UserId == userId));

    public Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = _items.Where(n => n.UserId == userId && !n.IsRead).ToList();
        foreach (var n in unread) n.MarkAsRead();
        return Task.FromResult(unread.Count);
    }
}

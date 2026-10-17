using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Domain.Repositories;

public interface IUserNotificationRepository
{
    Task AddAsync(UserNotification notification, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<UserNotification> Items, int Total)> GetFeedPageAsync(Guid userId, int page, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserNotification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

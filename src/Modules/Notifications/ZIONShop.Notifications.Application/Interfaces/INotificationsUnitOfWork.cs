namespace ZIONShop.Notifications.Application.Interfaces;

public interface INotificationsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

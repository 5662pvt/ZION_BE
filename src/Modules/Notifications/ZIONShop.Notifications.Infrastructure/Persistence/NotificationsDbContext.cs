using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Inbox;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Infrastructure.Persistence;

public class NotificationsDbContext : DbContext, INotificationsUnitOfWork
{
    public const string Schema = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.AddInbox(Schema);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}

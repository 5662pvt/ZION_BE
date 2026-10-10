using Microsoft.EntityFrameworkCore;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Infrastructure.Persistence;

public class NotificationsDbContext : DbContext, INotificationsUnitOfWork
{
    public const string Schema = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options) { }

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}

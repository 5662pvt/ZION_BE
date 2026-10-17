using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Infrastructure.Persistence.Configurations;

public class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.ToTable("UserNotifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(256).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.OrderId);
        builder.Property(n => n.IsRead).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();
        builder.Property(n => n.ReadAt);

        builder.HasIndex(n => new { n.UserId, n.CreatedAt }).IsDescending(false, true);
        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}

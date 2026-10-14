using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Notifications.Domain.Entities;

namespace ZIONShop.Notifications.Infrastructure.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.EventId).IsRequired();
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(l => l.Channel).HasMaxLength(16).IsRequired();
        builder.Property(l => l.UserId).IsRequired();
        builder.Property(l => l.OrderId).IsRequired();
        builder.Property(l => l.Recipient).HasMaxLength(256).IsRequired();
        builder.Property(l => l.Subject).HasMaxLength(256).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(l => l.Error).HasMaxLength(1000);
        builder.Property(l => l.CreatedAt).IsRequired();
        builder.Property(l => l.SentAt);

        builder.HasIndex(l => new { l.EventId, l.Type }).IsUnique();
        builder.HasIndex(l => l.OrderId);
    }
}

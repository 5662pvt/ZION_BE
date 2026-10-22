using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Chat.Domain.Entities;

namespace ZIONShop.Chat.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CustomerId).IsRequired();
        builder.HasIndex(c => c.CustomerId).IsUnique();

        builder.Property(c => c.CustomerName).HasMaxLength(256).IsRequired();
        builder.Property(c => c.CustomerEmail).HasMaxLength(256).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.LastMessageAt).IsRequired();
        builder.HasIndex(c => c.LastMessageAt);
        builder.Property(c => c.LastMessagePreview).HasMaxLength(120).IsRequired();
        builder.Property(c => c.LastMessageSender).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(c => c.CustomerUnread).IsRequired();
        builder.Property(c => c.StaffUnread).IsRequired();
        builder.Property(c => c.CustomerLastReadAt);
        builder.Property(c => c.StaffLastReadAt);

        // PostgreSQL optimistic concurrency via the system xmin column, same pattern as
        // Payments.Infrastructure.Persistence.Configurations.PaymentConfiguration -- two concurrent
        // AddMessage/MarkReadBy saves on the same conversation now conflict instead of silently
        // overwriting each other's unread-counter increment.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

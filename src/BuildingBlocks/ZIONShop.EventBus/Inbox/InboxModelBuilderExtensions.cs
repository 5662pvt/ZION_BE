using Microsoft.EntityFrameworkCore;

namespace ZIONShop.EventBus.Inbox;

public static class InboxModelBuilderExtensions
{
    public static void AddInbox(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages", schema);
            b.HasKey(x => new { x.MessageId, x.Consumer });
            b.Property(x => x.Consumer).IsRequired().HasMaxLength(256);
            b.Property(x => x.EventType).IsRequired().HasMaxLength(256);
            b.HasIndex(x => x.ProcessedOn);
        });
    }
}

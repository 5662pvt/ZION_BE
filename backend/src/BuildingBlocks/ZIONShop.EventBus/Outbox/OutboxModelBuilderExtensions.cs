using Microsoft.EntityFrameworkCore;

namespace ZIONShop.EventBus.Outbox;

public static class OutboxModelBuilderExtensions
{
    public static void AddOutbox(this ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages", schema);
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).IsRequired().HasMaxLength(512);
            b.Property(x => x.Content).IsRequired();
            b.Property(x => x.Error).HasMaxLength(2048);
            b.HasIndex(x => x.ProcessedOn);
        });
    }
}

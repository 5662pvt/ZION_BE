using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Chat.Domain.Entities;

namespace ZIONShop.Chat.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ConversationId).IsRequired();
        builder.Property(m => m.SenderId).IsRequired();
        builder.Property(m => m.SenderRole).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(m => m.Text).HasMaxLength(2000);
        builder.Property(m => m.ImageUrl).HasMaxLength(1024);
        builder.Property(m => m.ImagePublicId).HasMaxLength(512);
        builder.Property(m => m.ProductId);
        builder.Property(m => m.OrderId);
        builder.Property(m => m.CardJson).HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).IsRequired();

        // Covers the (CreatedAt, Id) keyset pagination tie-break used by GetMyMessages/GetConversationMessages.
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt, m.Id }).IsDescending(false, true, true);
    }
}

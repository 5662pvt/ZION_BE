using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Infrastructure.Persistence.Configurations;

public class OrderIdempotencyKeyConfiguration : IEntityTypeConfiguration<OrderIdempotencyKey>
{
    public const string UniqueIndexName = "IX_idempotency_keys_UserId_Key";

    public void Configure(EntityTypeBuilder<OrderIdempotencyKey> builder)
    {
        builder.ToTable("idempotency_keys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.UserId).IsRequired();
        builder.Property(k => k.Key).IsRequired().HasMaxLength(100);
        builder.Property(k => k.RequestHash).IsRequired().HasMaxLength(64);
        builder.Property(k => k.OrderId).IsRequired();
        builder.Property(k => k.CreatedAt).IsRequired();

        builder.HasIndex(k => new { k.UserId, k.Key }).IsUnique().HasDatabaseName(UniqueIndexName);
        builder.HasIndex(k => k.CreatedAt);
    }
}

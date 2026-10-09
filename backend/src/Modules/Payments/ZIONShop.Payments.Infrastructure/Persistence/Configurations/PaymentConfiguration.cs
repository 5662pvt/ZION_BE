using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.OrderId).IsRequired();
        builder.HasIndex(p => p.OrderId);

        builder.Property(p => p.UserId).IsRequired();

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.Provider).IsRequired().HasMaxLength(32);

        builder.Property(p => p.IntentId).IsRequired().HasMaxLength(64);
        builder.HasIndex(p => p.IntentId).IsUnique();

        builder.Property(p => p.IdempotencyKey).IsRequired().HasMaxLength(100);
        builder.HasIndex(p => new { p.UserId, p.IdempotencyKey }).IsUnique();

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.ExpiresAt).IsRequired();
        builder.Property(p => p.CompletedAt);

        builder.Property(p => p.CreatedDate).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.LastModifiedDate);
        builder.Property(p => p.LastModifiedBy).HasMaxLength(128);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Ignore(p => p.RowVersion);
        builder.Ignore(p => p.DomainEvents);
        builder.HasQueryFilter(p => !p.IsDeleted);

        // PostgreSQL optimistic concurrency via the system xmin column, same pattern as OrdersInfrastructure.Persistence.Configurations.OrderConfiguration.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

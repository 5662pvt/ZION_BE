using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrderId).IsRequired();
        builder.HasIndex(r => r.OrderId);

        builder.Property(r => r.UserId).IsRequired();

        builder.Property(r => r.ReturnRequestId).IsRequired();
        builder.HasIndex(r => r.ReturnRequestId).IsUnique();

        builder.Property(r => r.PaymentId);

        builder.Property(r => r.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(r => r.Currency).IsRequired().HasMaxLength(3);
        builder.Property(r => r.Method).IsRequired().HasMaxLength(16);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.ProviderRefundId).HasMaxLength(64);
        builder.Property(r => r.FailureReason).HasMaxLength(500);
        builder.Property(r => r.CompletedAt);

        builder.Property(r => r.CreatedDate).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(128);
        builder.Property(r => r.LastModifiedDate);
        builder.Property(r => r.LastModifiedBy).HasMaxLength(128);
        builder.Property(r => r.IsDeleted).HasDefaultValue(false);
        builder.Ignore(r => r.RowVersion);
        builder.Ignore(r => r.DomainEvents);
        builder.HasQueryFilter(r => !r.IsDeleted);

        // PostgreSQL optimistic concurrency via the system xmin column, same pattern as PaymentConfiguration.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Infrastructure.Persistence.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    /// <summary>Enforces "at most one non-Rejected return request per order" at the database level via a
    /// partial unique index; <see cref="OrdersDbContext.SaveChangesAsync"/> translates a violation into
    /// <see cref="Application.Exceptions.DuplicateReturnRequestException"/>.</summary>
    public const string ActiveRequestUniqueIndexName = "IX_ReturnRequests_OrderId_Active";

    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("ReturnRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrderId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.RejectReason).HasMaxLength(500);
        builder.Property(r => r.ReturnTrackingCode).HasMaxLength(100);
        builder.Property(r => r.RefundAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(r => r.RefundMethod).HasMaxLength(16);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasIndex(r => r.OrderId)
            .IsUnique()
            .HasDatabaseName(ActiveRequestUniqueIndexName)
            .HasFilter("\"Status\" <> 'Rejected'");
    }
}

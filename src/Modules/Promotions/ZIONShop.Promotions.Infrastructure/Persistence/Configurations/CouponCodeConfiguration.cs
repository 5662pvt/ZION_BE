using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class CouponCodeConfiguration : IEntityTypeConfiguration<CouponCode>
{
    public void Configure(EntityTypeBuilder<CouponCode> builder)
    {
        builder.ToTable("CouponCodes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PromotionId).IsRequired();
        builder.HasIndex(c => c.PromotionId);

        builder.Property(c => c.Code).IsRequired().HasMaxLength(30);
        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(c => c.UsedCount).IsRequired().HasDefaultValue(0);
        builder.Property(c => c.CreatedAt).IsRequired();

        // PostgreSQL optimistic concurrency via the system xmin column — guards per-coupon UsedCount increments.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

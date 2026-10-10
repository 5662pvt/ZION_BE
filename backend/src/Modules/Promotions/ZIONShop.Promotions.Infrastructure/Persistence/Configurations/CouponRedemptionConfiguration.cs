using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("CouponRedemptions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PromotionId).IsRequired();
        builder.Property(r => r.CouponCodeId).IsRequired();
        builder.Property(r => r.OrderId).IsRequired();
        builder.HasIndex(r => r.OrderId).IsUnique();

        builder.Property(r => r.UserId).IsRequired();
        builder.HasIndex(r => new { r.PromotionId, r.UserId });

        builder.Property(r => r.DiscountAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);
    }
}

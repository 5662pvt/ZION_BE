using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class GiftRedemptionConfiguration : IEntityTypeConfiguration<GiftRedemption>
{
    public void Configure(EntityTypeBuilder<GiftRedemption> builder)
    {
        builder.ToTable("GiftRedemptions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PromotionId).IsRequired();
        builder.Property(r => r.OrderId).IsRequired();
        builder.HasIndex(r => new { r.PromotionId, r.OrderId }).IsUnique();

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(r => new { r.PromotionId, r.UserId, r.Status });

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);
    }
}

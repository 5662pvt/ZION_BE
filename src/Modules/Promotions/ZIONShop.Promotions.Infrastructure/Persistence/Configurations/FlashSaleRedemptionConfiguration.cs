using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class FlashSaleRedemptionConfiguration : IEntityTypeConfiguration<FlashSaleRedemption>
{
    public void Configure(EntityTypeBuilder<FlashSaleRedemption> builder)
    {
        builder.ToTable("FlashSaleRedemptions");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.FlashSaleItemId).IsRequired();
        builder.Property(r => r.OrderId).IsRequired();
        builder.HasIndex(r => new { r.FlashSaleItemId, r.OrderId }).IsUnique();

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Quantity).IsRequired();
        builder.Property(r => r.UnitFlashPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(r => new { r.Status, r.CreatedAt });

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);
    }
}

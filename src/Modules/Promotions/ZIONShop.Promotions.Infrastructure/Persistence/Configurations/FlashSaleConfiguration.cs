using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class FlashSaleConfiguration : IEntityTypeConfiguration<FlashSale>
{
    public void Configure(EntityTypeBuilder<FlashSale> builder)
    {
        builder.ToTable("FlashSales");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).IsRequired().HasMaxLength(200);
        builder.Property(f => f.StartsAt).IsRequired();
        builder.Property(f => f.EndsAt).IsRequired();
        builder.Property(f => f.IsActive).IsRequired().HasDefaultValue(false);
        builder.HasIndex(f => f.IsActive);
    }
}

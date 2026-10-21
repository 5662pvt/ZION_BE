using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class FlashSaleItemConfiguration : IEntityTypeConfiguration<FlashSaleItem>
{
    public void Configure(EntityTypeBuilder<FlashSaleItem> builder)
    {
        builder.ToTable("FlashSaleItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.FlashSaleId).IsRequired();
        builder.HasIndex(i => i.FlashSaleId);

        builder.Property(i => i.ProductId).IsRequired();
        builder.HasIndex(i => i.ProductId);

        builder.Property(i => i.VariantId).IsRequired();
        builder.HasIndex(i => i.VariantId);
        builder.HasIndex(i => new { i.FlashSaleId, i.VariantId }).IsUnique();

        builder.Property(i => i.FlashPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.AllocatedQuantity).IsRequired();
        builder.Property(i => i.SoldQuantity).IsRequired().HasDefaultValue(0);

        builder.Ignore(i => i.Available);
    }
}

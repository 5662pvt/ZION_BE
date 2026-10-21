using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.ProductId).IsRequired();
        builder.HasIndex(v => v.ProductId);

        builder.Property(v => v.Option1Value).HasMaxLength(30);
        builder.Property(v => v.Option2Value).HasMaxLength(30);
        builder.Property(v => v.ImageId);
        builder.Property(v => v.IsActive).IsRequired();
        builder.Property(v => v.DisplayOrder).IsRequired();

        builder.OwnsOne(v => v.Sku, sku =>
        {
            sku.Property(s => s.Value).HasColumnName("Sku").HasMaxLength(64).IsRequired();
            sku.HasIndex(s => s.Value).IsUnique();
        });

        builder.OwnsOne(v => v.Price, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Price").HasColumnType("decimal(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });

        builder.Ignore(v => v.Label);
    }
}

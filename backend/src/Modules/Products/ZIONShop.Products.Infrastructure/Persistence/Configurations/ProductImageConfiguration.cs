using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Infrastructure.Persistence.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.Url).IsRequired().HasMaxLength(2048);
        builder.Property(i => i.Alt).HasMaxLength(256);
        builder.Property(i => i.DisplayOrder);
    }
}

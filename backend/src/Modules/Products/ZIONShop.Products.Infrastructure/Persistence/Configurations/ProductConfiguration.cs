using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;

namespace ZIONShop.Products.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Slug).IsRequired().HasMaxLength(256);
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.Property(p => p.Description).HasMaxLength(4000);
        builder.Property(p => p.Brand).HasMaxLength(128);
        builder.Property(p => p.Status).HasConversion<int>().IsRequired();
        builder.Property(p => p.CategoryId);

        builder.OwnsOne(p => p.Sku, sku =>
        {
            sku.Property(s => s.Value).HasColumnName("Sku").HasMaxLength(64).IsRequired();
            sku.HasIndex(s => s.Value).IsUnique();
        });

        builder.OwnsOne(p => p.Price, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Price").HasColumnType("decimal(18,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3).IsRequired();
        });

        builder.Property(p => p.CreatedDate).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.LastModifiedDate);
        builder.Property(p => p.LastModifiedBy).HasMaxLength(128);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Ignore(p => p.DomainEvents);
        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Product.Images))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

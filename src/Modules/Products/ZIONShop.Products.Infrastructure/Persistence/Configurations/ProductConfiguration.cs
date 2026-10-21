using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Products.Domain.Entities;

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
        builder.Property(p => p.MinPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.MaxPrice).HasColumnType("decimal(18,2)").IsRequired();

        builder.Property(p => p.CreatedDate).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.LastModifiedDate);
        builder.Property(p => p.LastModifiedBy).HasMaxLength(128);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Ignore(p => p.RowVersion);

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.DefaultVariant);
        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Product.Images))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Product.Variants))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(p => p.Options, option =>
        {
            option.ToTable("ProductOptions");
            option.WithOwner().HasForeignKey("ProductId");
            option.Property<Guid>("ProductId");
            option.HasKey("ProductId", nameof(ProductOption.Position));
            option.Property(o => o.Position).IsRequired();
            option.Property(o => o.Name).IsRequired().HasMaxLength(30);
            option.Property(o => o.Values).HasColumnName("Values").HasColumnType("text[]").IsRequired();
        });

        builder.Metadata.FindNavigation(nameof(Product.Options))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

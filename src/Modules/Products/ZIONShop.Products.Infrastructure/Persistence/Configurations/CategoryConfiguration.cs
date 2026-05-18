using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(128);
        builder.Property(c => c.Slug).IsRequired().HasMaxLength(128);
        builder.HasIndex(c => c.Slug).IsUnique();
        builder.Property(c => c.Description).HasMaxLength(1024);
        builder.Property(c => c.ParentId);
        builder.Property(c => c.DisplayOrder);

        builder.Property(c => c.CreatedDate).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(128);
        builder.Property(c => c.LastModifiedDate);
        builder.Property(c => c.LastModifiedBy).HasMaxLength(128);
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

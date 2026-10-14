using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Infrastructure.Persistence.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ProductId).IsRequired();
        builder.HasIndex(i => i.ProductId).IsUnique();
        builder.Property(i => i.Sku).IsRequired().HasMaxLength(64);
        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(256);
        builder.Property(i => i.OnHand).IsRequired();
        builder.Property(i => i.Reserved).IsRequired();
        builder.Property(i => i.LowStockThreshold).IsRequired();
        builder.Ignore(i => i.Available);
        builder.Ignore(i => i.IsLowStock);
        builder.Ignore(i => i.RowVersion);
        builder.Ignore(i => i.DomainEvents);

        builder.Property(i => i.CreatedDate).IsRequired();
        builder.Property(i => i.CreatedBy).HasMaxLength(128);
        builder.Property(i => i.LastModifiedDate);
        builder.Property(i => i.LastModifiedBy).HasMaxLength(128);
        builder.Property(i => i.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(i => !i.IsDeleted);

        // PostgreSQL optimistic concurrency via the system xmin column — no extra column needed,
        // and it's what protects the "last unit, two concurrent reservations" race in Reserve/Commit.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

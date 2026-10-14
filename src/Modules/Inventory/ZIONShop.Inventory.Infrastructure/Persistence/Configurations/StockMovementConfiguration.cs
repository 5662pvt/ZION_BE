using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Inventory.Domain.Entities;

namespace ZIONShop.Inventory.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ProductId).IsRequired();
        builder.Property(m => m.Type).HasConversion<int>().IsRequired();
        builder.Property(m => m.Quantity).IsRequired();
        builder.Property(m => m.Reference).HasMaxLength(256);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.CreatedBy).HasMaxLength(128);

        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });
    }
}

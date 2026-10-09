using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Infrastructure.Persistence.Configurations;

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.OrderId).IsRequired();
        builder.Property(l => l.ProductId).IsRequired();
        builder.Property(l => l.ProductName).IsRequired().HasMaxLength(256);
        builder.Property(l => l.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(l => l.Quantity).IsRequired();
        builder.Ignore(l => l.LineTotal);
        builder.HasIndex(l => new { l.OrderId, l.ProductId });
    }
}

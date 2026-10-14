using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Infrastructure.Persistence.Configurations;

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.OrderId).IsRequired();
        builder.HasIndex(h => h.OrderId);
        builder.HasIndex(h => new { h.Status, h.At });
        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(h => h.At).IsRequired();
        builder.Property(h => h.Note).HasMaxLength(500);
    }
}

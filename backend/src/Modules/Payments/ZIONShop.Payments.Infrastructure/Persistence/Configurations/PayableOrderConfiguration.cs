using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Infrastructure.Persistence.Configurations;

public class PayableOrderConfiguration : IEntityTypeConfiguration<PayableOrder>
{
    public void Configure(EntityTypeBuilder<PayableOrder> builder)
    {
        builder.ToTable("PayableOrders");
        builder.HasKey(p => p.OrderId);

        builder.Property(p => p.UserId).IsRequired();
        builder.HasIndex(p => p.UserId);

        builder.Property(p => p.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
        builder.Property(p => p.State).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(p => p.UpdatedAt).IsRequired();
    }
}

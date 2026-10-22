using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Infrastructure.Persistence.Configurations;

public class OrderPointsRevocationConfiguration : IEntityTypeConfiguration<OrderPointsRevocation>
{
    public void Configure(EntityTypeBuilder<OrderPointsRevocation> builder)
    {
        builder.ToTable("OrderPointsRevocations");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrderId).IsRequired();
        builder.HasIndex(r => r.OrderId).IsUnique();

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
    }
}

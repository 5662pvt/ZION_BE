using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Infrastructure.Persistence.Configurations;

public class PointReservationConfiguration : IEntityTypeConfiguration<PointReservation>
{
    public void Configure(EntityTypeBuilder<PointReservation> builder)
    {
        builder.ToTable("PointReservations");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.OrderId).IsRequired();
        builder.HasIndex(r => r.OrderId).IsUnique();

        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.Points).IsRequired();
        builder.Property(r => r.DiscountAmount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);

        builder.OwnsMany(r => r.Allocations, a =>
        {
            a.ToTable("PointReservationAllocations");
            a.WithOwner().HasForeignKey(x => x.PointReservationId);
            a.HasKey(x => new { x.PointReservationId, x.LotId });
            a.Property(x => x.Points).IsRequired();
        });
        builder.Navigation(r => r.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

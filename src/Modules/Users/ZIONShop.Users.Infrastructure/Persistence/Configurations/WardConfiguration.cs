using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Infrastructure.Persistence.Configurations;

public class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("Wards");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Code).IsRequired().HasMaxLength(10);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(150);
        builder.Property(w => w.ProvinceId).IsRequired();
        builder.HasIndex(w => new { w.ProvinceId, w.Code }).IsUnique();
        builder.HasOne<Province>().WithMany().HasForeignKey(w => w.ProvinceId).OnDelete(DeleteBehavior.Cascade);
    }
}

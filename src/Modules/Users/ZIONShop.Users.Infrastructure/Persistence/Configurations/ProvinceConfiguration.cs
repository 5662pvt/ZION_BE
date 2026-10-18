using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Infrastructure.Persistence.Configurations;

public class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.ToTable("Provinces");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(10);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(p => p.Code).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("Addresses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserProfileId).IsRequired();
        builder.Property(a => a.Line1).IsRequired().HasMaxLength(256);
        builder.Property(a => a.Line2).HasMaxLength(256);
        builder.Property(a => a.City).IsRequired().HasMaxLength(128);
        builder.Property(a => a.State).HasMaxLength(128);
        builder.Property(a => a.Country).IsRequired().HasMaxLength(64);
        builder.Property(a => a.PostalCode).IsRequired().HasMaxLength(16);
        builder.Property(a => a.IsDefault).IsRequired();
    }
}

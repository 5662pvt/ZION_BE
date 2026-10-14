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
        builder.Property(a => a.RecipientName).IsRequired().HasMaxLength(100);
        builder.Property(a => a.PhoneNumber).IsRequired().HasMaxLength(15);
        builder.Property(a => a.Province).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Ward).IsRequired().HasMaxLength(100);
        builder.Property(a => a.StreetAddress).IsRequired().HasMaxLength(255);
        builder.Property(a => a.IsDefault).IsRequired();
    }
}

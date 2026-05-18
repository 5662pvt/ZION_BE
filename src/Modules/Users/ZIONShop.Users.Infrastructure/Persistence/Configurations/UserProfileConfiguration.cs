using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Users.Domain.Entities;

namespace ZIONShop.Users.Infrastructure.Persistence.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.AuthUserId).IsRequired();
        builder.HasIndex(p => p.AuthUserId).IsUnique();
        builder.Property(p => p.Email).IsRequired().HasMaxLength(256);
        builder.Property(p => p.FullName).HasMaxLength(128);
        builder.Property(p => p.PhoneNumber).HasMaxLength(32);
        builder.Property(p => p.DateOfBirth);

        builder.Property(p => p.CreatedDate).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.LastModifiedDate);
        builder.Property(p => p.LastModifiedBy).HasMaxLength(128);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Property(p => p.RowVersion).IsRowVersion();

        builder.Ignore(p => p.DomainEvents);
        builder.HasQueryFilter(p => !p.IsDeleted);

        builder.HasMany(p => p.Addresses)
            .WithOne()
            .HasForeignKey(a => a.UserProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(UserProfile.Addresses))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

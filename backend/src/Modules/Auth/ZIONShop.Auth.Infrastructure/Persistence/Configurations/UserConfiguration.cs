using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Auth.Domain.Entities;

namespace ZIONShop.Auth.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
        builder.Property(u => u.DisplayName).HasMaxLength(128);
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.LastLoginAt);

        builder.Property(u => u.RolesRaw)
            .HasColumnName("Roles")
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.CreatedDate).IsRequired();
        builder.Property(u => u.CreatedBy).HasMaxLength(128);
        builder.Property(u => u.LastModifiedDate);
        builder.Property(u => u.LastModifiedBy).HasMaxLength(128);
        builder.Property(u => u.IsDeleted).HasDefaultValue(false);
        builder.Property(u => u.RowVersion).IsRowVersion();

        builder.Ignore(u => u.DomainEvents);
        builder.Ignore(u => u.Roles);

        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(User.RefreshTokens))!
            .SetPropertyAccessMode(Microsoft.EntityFrameworkCore.PropertyAccessMode.Field);
    }
}

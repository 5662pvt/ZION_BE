using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Auth.Domain.Entities;

namespace ZIONShop.Auth.Infrastructure.Persistence.Configurations;

public class AuthOtpConfiguration : IEntityTypeConfiguration<AuthOtp>
{
    public void Configure(EntityTypeBuilder<AuthOtp> builder)
    {
        builder.ToTable("AuthOtps");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Email).IsRequired().HasMaxLength(256);
        builder.Property(o => o.Purpose).IsRequired();
        builder.Property(o => o.CodeHash).IsRequired().HasMaxLength(128);
        builder.Property(o => o.ExpiresAt).IsRequired();
        builder.Property(o => o.CreatedDate).IsRequired();
        builder.HasIndex(o => new { o.Email, o.Purpose, o.UsedAt });
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Infrastructure.Persistence.Configurations;

public class PointAccountConfiguration : IEntityTypeConfiguration<PointAccount>
{
    public void Configure(EntityTypeBuilder<PointAccount> builder)
    {
        builder.ToTable("PointAccounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("UserId");

        builder.Property(a => a.Balance).IsRequired().HasDefaultValue(0);

        // PostgreSQL optimistic concurrency via the system xmin column — guards concurrent earn/spend/release
        // mutations of Balance on the same account.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

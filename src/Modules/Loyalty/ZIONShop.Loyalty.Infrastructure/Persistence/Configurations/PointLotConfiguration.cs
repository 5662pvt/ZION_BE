using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;

namespace ZIONShop.Loyalty.Infrastructure.Persistence.Configurations;

public class PointLotConfiguration : IEntityTypeConfiguration<PointLot>
{
    public void Configure(EntityTypeBuilder<PointLot> builder)
    {
        builder.ToTable("PointLots");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.UserId).IsRequired();
        builder.Property(l => l.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(l => l.SourceRef).IsRequired().HasMaxLength(64);
        builder.Property(l => l.Original).IsRequired();
        builder.Property(l => l.Remaining).IsRequired();
        builder.Property(l => l.EarnedAt).IsRequired();
        builder.Property(l => l.ExpiresAt).IsRequired();

        builder.HasIndex(l => new { l.UserId, l.Remaining, l.ExpiresAt });
        builder.HasIndex(l => l.ExpiresAt);

        // Idempotency for earn handlers: one lot per (UserId, Source, SourceRef) for Order/Review sources only
        // (AdminAdjust/Restore lots are not deduplicated — each admin adjustment or give-back is its own lot).
        builder.HasIndex(l => new { l.UserId, l.Source, l.SourceRef })
            .IsUnique()
            .HasFilter($"\"Source\" IN ('{PointLotSource.Order}', '{PointLotSource.Review}')");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Infrastructure.Persistence.Configurations;

public class PointTransactionConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(t => t.Points).IsRequired();
        builder.Property(t => t.BalanceAfter).IsRequired();
        builder.Property(t => t.OrderId);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(500);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.ActorId);

        builder.HasIndex(t => new { t.UserId, t.CreatedAt });
        builder.HasIndex(t => t.OrderId);
    }
}

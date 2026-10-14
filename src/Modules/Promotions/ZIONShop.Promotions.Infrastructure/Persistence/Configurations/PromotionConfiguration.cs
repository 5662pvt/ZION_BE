using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;

namespace ZIONShop.Promotions.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("Promotions");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Value).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.MaxDiscountAmount).HasColumnType("decimal(18,2)");
        builder.Property(p => p.MinOrderSubtotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.StartsAt).IsRequired();
        builder.Property(p => p.EndsAt).IsRequired();
        builder.Property(p => p.IsActive).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.UsageLimit);
        builder.Property(p => p.PerUserLimit);
        builder.Property(p => p.UsedCount).IsRequired().HasDefaultValue(0);
        builder.Property(p => p.Scope).HasConversion<string>().HasMaxLength(16).IsRequired().HasDefaultValue(PromotionScope.Order);
        builder.Property(p => p.TargetProductIds).HasColumnType("uuid[]").IsRequired().HasDefaultValueSql("'{}'");
        builder.Property(p => p.TargetCategoryIds).HasColumnType("uuid[]").IsRequired().HasDefaultValueSql("'{}'");
        builder.Property(p => p.MinQuantity);
        builder.HasIndex(p => p.IsActive);

        builder.OwnsMany(p => p.GiftItems, gi =>
        {
            gi.ToTable("promotion_gift_items");
            gi.WithOwner().HasForeignKey(x => x.PromotionId);
            gi.HasKey(x => new { x.PromotionId, x.ProductId });
            gi.Property(x => x.ProductId).IsRequired();
            gi.Property(x => x.Quantity).IsRequired();
        });
        builder.Navigation(p => p.GiftItems).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(p => p.CreatedDate).IsRequired();
        builder.Property(p => p.CreatedBy).HasMaxLength(128);
        builder.Property(p => p.LastModifiedDate);
        builder.Property(p => p.LastModifiedBy).HasMaxLength(128);
        builder.Property(p => p.IsDeleted).HasDefaultValue(false);
        builder.Ignore(p => p.RowVersion);
        builder.Ignore(p => p.DomainEvents);
        builder.HasQueryFilter(p => !p.IsDeleted);

        // PostgreSQL optimistic concurrency via the system xmin column — guards UsedCount increments
        // (and the overall usage limit) against concurrent coupon reservations for the same promotion.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

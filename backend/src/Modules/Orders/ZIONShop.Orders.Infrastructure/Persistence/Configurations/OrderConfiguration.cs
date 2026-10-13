using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Code).IsRequired().HasMaxLength(16);
        builder.HasIndex(o => o.Code).IsUnique();

        builder.Property(o => o.UserId).IsRequired();
        builder.HasIndex(o => o.UserId);
        builder.HasIndex(o => o.CreatedDate);
        builder.HasIndex(o => o.Status);

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(o => o.Currency).IsRequired().HasMaxLength(3);
        builder.Property(o => o.Subtotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.ShippingFee).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.CouponCode).HasMaxLength(30);
        builder.Property(o => o.DiscountAmount).HasColumnType("decimal(18,2)").IsRequired().HasDefaultValue(0m);
        builder.Property(o => o.DiscountTarget).HasColumnName("discount_target").HasMaxLength(16);
        builder.Ignore(o => o.Total);
        builder.Property(o => o.Note).HasMaxLength(1000);
        builder.Property(o => o.CancelReason).HasMaxLength(500);

        builder.OwnsOne(o => o.ShippingAddress, sa =>
        {
            sa.Property(a => a.AddressId).HasColumnName("ShippingAddressId").IsRequired();
            sa.Property(a => a.RecipientName).HasColumnName("ShippingRecipientName").HasMaxLength(128).IsRequired();
            sa.Property(a => a.PhoneNumber).HasColumnName("ShippingPhoneNumber").HasMaxLength(32).IsRequired();
            sa.Property(a => a.Province).HasColumnName("ShippingProvince").HasMaxLength(128).IsRequired();
            sa.Property(a => a.Ward).HasColumnName("ShippingWard").HasMaxLength(128).IsRequired();
            sa.Property(a => a.StreetAddress).HasColumnName("ShippingStreetAddress").HasMaxLength(256).IsRequired();
        });
        builder.Navigation(o => o.ShippingAddress).IsRequired();

        builder.Property(o => o.CreatedDate).IsRequired();
        builder.Property(o => o.CreatedBy).HasMaxLength(128);
        builder.Property(o => o.LastModifiedDate);
        builder.Property(o => o.LastModifiedBy).HasMaxLength(128);
        builder.Property(o => o.IsDeleted).HasDefaultValue(false);
        builder.Ignore(o => o.RowVersion);
        builder.Ignore(o => o.DomainEvents);
        builder.HasQueryFilter(o => !o.IsDeleted);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Order.Lines))!.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(o => o.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(Order.StatusHistory))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

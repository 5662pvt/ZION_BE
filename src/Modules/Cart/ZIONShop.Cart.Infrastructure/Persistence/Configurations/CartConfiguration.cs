using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<DomainCart>
{
    public void Configure(EntityTypeBuilder<DomainCart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.UserId);
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");
        builder.Property(c => c.AnonymousId).HasMaxLength(64);
        builder.HasIndex(c => c.AnonymousId).IsUnique().HasFilter("[AnonymousId] IS NOT NULL");
        builder.Property(c => c.ExpiresAt).IsRequired();

        builder.Property(c => c.CreatedDate).IsRequired();
        builder.Property(c => c.CreatedBy).HasMaxLength(128);
        builder.Property(c => c.LastModifiedDate);
        builder.Property(c => c.LastModifiedBy).HasMaxLength(128);
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);
        builder.Property(c => c.RowVersion).IsRowVersion();

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Subtotal);
        builder.Ignore(c => c.TotalQuantity);

        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(DomainCart.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}

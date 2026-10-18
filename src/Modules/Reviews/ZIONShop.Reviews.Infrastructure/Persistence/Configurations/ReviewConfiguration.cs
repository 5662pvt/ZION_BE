using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ProductId).IsRequired();
        builder.Property(r => r.UserId).IsRequired();
        builder.Property(r => r.OrderId).IsRequired();
        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Title).HasMaxLength(120);
        builder.Property(r => r.Content).HasMaxLength(2000).IsRequired();
        builder.Property(r => r.AuthorName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.ProductName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(r => r.HiddenReason).HasMaxLength(200);
        builder.Property(r => r.ReplyContent).HasMaxLength(2000);
        builder.Property(r => r.RepliedAt);

        builder.Property(r => r.CreatedDate).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(128);
        builder.Property(r => r.LastModifiedDate);
        builder.Property(r => r.LastModifiedBy).HasMaxLength(128);
        builder.Property(r => r.IsDeleted).HasDefaultValue(false);
        builder.Ignore(r => r.RowVersion);
        builder.Ignore(r => r.DomainEvents);
        builder.HasQueryFilter(r => !r.IsDeleted);

        builder.HasIndex(r => new { r.UserId, r.ProductId }).IsUnique();
        builder.HasIndex(r => new { r.ProductId, r.Status });
    }
}

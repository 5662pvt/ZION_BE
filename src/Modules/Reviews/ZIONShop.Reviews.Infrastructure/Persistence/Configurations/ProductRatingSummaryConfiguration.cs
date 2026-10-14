using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Infrastructure.Persistence.Configurations;

public class ProductRatingSummaryConfiguration : IEntityTypeConfiguration<ProductRatingSummary>
{
    public void Configure(EntityTypeBuilder<ProductRatingSummary> builder)
    {
        builder.ToTable("ProductRatingSummaries");
        builder.HasKey(s => s.ProductId);

        builder.Property(s => s.Count).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.Sum).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.Star1).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.Star2).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.Star3).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.Star4).IsRequired().HasDefaultValue(0);
        builder.Property(s => s.Star5).IsRequired().HasDefaultValue(0);
        builder.Ignore(s => s.Average);

        // PostgreSQL optimistic concurrency via the system xmin column — guards the recomputed
        // counters against lost updates when two reviews for the same product are mutated concurrently.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();
    }
}

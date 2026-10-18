using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Infrastructure.Persistence;

public class ReviewsDbContext : DbContext, IReviewsUnitOfWork
{
    public const string Schema = "reviews";

    public ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : base(options) { }

    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProductRatingSummary> ProductRatingSummaries => Set<ProductRatingSummary>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewsDbContext).Assembly);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}

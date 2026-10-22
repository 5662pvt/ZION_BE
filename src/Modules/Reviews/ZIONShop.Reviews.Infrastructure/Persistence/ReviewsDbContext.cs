using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Infrastructure.Persistence;

public class ReviewsDbContext : DbContext, IReviewsUnitOfWork
{
    public const string Schema = "reviews";

    private readonly Outbox<ReviewsDbContext> _outbox;

    public ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : base(options)
    {
        _outbox = new Outbox<ReviewsDbContext>(this);
    }

    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProductRatingSummary> ProductRatingSummaries => Set<ProductRatingSummary>();
    public DbSet<ProductQuestion> ProductQuestions => Set<ProductQuestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewsDbContext).Assembly);
        modelBuilder.AddOutbox(Schema);
    }

    public void AddOutboxEvent(IntegrationEvent @event) => _outbox.Add(@event);

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}

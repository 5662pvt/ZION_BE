using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.UpdateReview;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class UpdateReviewCommandHandlerTests
{
    private static (ReviewsDbContext db, ReviewRepository reviews, ProductRatingSummaryUpdater updater) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ReviewsDbContext(options);
        var reviews = new ReviewRepository(db);
        var summaries = new ProductRatingSummaryRepository(db);
        return (db, reviews, new ProductRatingSummaryUpdater(reviews, summaries, db));
    }

    [Fact]
    public async Task Handle_Should_UpdateRatingAndRecomputeSummary_When_OwnerEdits()
    {
        var (db, reviews, updater) = BuildScope();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var review = Review.Create(productId, userId, Guid.NewGuid(), 2, null, "Not great initially, too slow to arrive.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await updater.ApplyAsync(productId, review.Id, review.Rating, CancellationToken.None);

        var handler = new UpdateReviewCommandHandler(reviews, updater);
        var cmd = new UpdateReviewCommand(review.Id, userId, 5, "Changed my mind", "Actually it is great, works perfectly now.");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(5);

        var summary = await db.ProductRatingSummaries.FirstAsync(s => s.ProductId == productId);
        summary.Count.Should().Be(1);
        summary.Average.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_CallerIsNotOwner()
    {
        var (db, reviews, updater) = BuildScope();
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, null, "A review written by someone else entirely.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await db.SaveChangesAsync();

        var handler = new UpdateReviewCommandHandler(reviews, updater);
        var cmd = new UpdateReviewCommand(review.Id, Guid.NewGuid(), 5, null, "Trying to edit someone else's review here.");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ReviewNotFound");
    }
}

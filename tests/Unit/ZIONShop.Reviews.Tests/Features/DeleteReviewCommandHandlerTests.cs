using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.DeleteReview;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class DeleteReviewCommandHandlerTests
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
    public async Task Handle_Should_RemoveReviewAndZeroOutSummary_When_OwnerDeletesOnlyReview()
    {
        var (db, reviews, updater) = BuildScope();
        var productId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var review = Review.Create(productId, userId, Guid.NewGuid(), 4, null, "A review that will shortly be deleted.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await updater.ApplyAsync(productId, review.Id, review.Rating, CancellationToken.None);

        var handler = new DeleteReviewCommandHandler(reviews, updater);
        var result = await handler.Handle(new DeleteReviewCommand(review.Id, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Reviews.FindAsync(review.Id)).Should().BeNull();

        var summary = await db.ProductRatingSummaries.FirstAsync(s => s.ProductId == productId);
        summary.Count.Should().Be(0);
        summary.Average.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_CallerIsNotOwner()
    {
        var (db, reviews, updater) = BuildScope();
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 3, null, "A review owned by a different user.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await db.SaveChangesAsync();

        var handler = new DeleteReviewCommandHandler(reviews, updater);
        var result = await handler.Handle(new DeleteReviewCommand(review.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ReviewNotFound");
    }
}

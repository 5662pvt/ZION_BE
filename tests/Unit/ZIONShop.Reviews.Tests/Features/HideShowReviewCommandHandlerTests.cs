using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.HideReview;
using ZIONShop.Reviews.Application.Features.ShowReview;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class HideShowReviewCommandHandlerTests
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
    public async Task Hide_Should_RemoveReviewFromSummary_When_TwoReviewsExistForProduct()
    {
        var (db, reviews, updater) = BuildScope();
        var productId = Guid.NewGuid();
        var reviewA = Review.Create(productId, Guid.NewGuid(), Guid.NewGuid(), 5, null, "A five star review for this product.", "ng***", "Test Product");
        var reviewB = Review.Create(productId, Guid.NewGuid(), Guid.NewGuid(), 1, null, "A one star review for this product.", "ab***", "Test Product");
        await db.Reviews.AddRangeAsync(reviewA, reviewB);
        await updater.ApplyAsync(productId, reviewA.Id, reviewA.Rating, CancellationToken.None);
        await updater.ApplyAsync(productId, reviewB.Id, reviewB.Rating, CancellationToken.None);

        var handler = new HideReviewCommandHandler(reviews, updater);
        var result = await handler.Handle(new HideReviewCommand(reviewB.Id, "Spam"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Hidden");

        var summary = await db.ProductRatingSummaries.FirstAsync(s => s.ProductId == productId);
        summary.Count.Should().Be(1);
        summary.Average.Should().Be(5m);
    }

    [Fact]
    public async Task Show_Should_RestoreReviewToSummary_When_PreviouslyHidden()
    {
        var (db, reviews, updater) = BuildScope();
        var productId = Guid.NewGuid();
        var review = Review.Create(productId, Guid.NewGuid(), Guid.NewGuid(), 3, null, "A review that will be hidden then shown.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await updater.ApplyAsync(productId, review.Id, review.Rating, CancellationToken.None);

        var hideHandler = new HideReviewCommandHandler(reviews, updater);
        await hideHandler.Handle(new HideReviewCommand(review.Id, "Review"), CancellationToken.None);

        var hiddenSummary = await db.ProductRatingSummaries.FirstAsync(s => s.ProductId == productId);
        hiddenSummary.Count.Should().Be(0);

        var showHandler = new ShowReviewCommandHandler(reviews, updater);
        var result = await showHandler.Handle(new ShowReviewCommand(review.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Published");

        var restoredSummary = await db.ProductRatingSummaries.FirstAsync(s => s.ProductId == productId);
        restoredSummary.Count.Should().Be(1);
        restoredSummary.Average.Should().Be(3m);
    }

    [Fact]
    public async Task Hide_Should_ReturnNotFound_When_ReviewDoesNotExist()
    {
        var (_, reviews, updater) = BuildScope();
        var handler = new HideReviewCommandHandler(reviews, updater);

        var result = await handler.Handle(new HideReviewCommand(Guid.NewGuid(), null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ReviewNotFound");
    }
}

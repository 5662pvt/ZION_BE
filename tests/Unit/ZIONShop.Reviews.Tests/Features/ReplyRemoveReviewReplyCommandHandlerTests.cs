using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.RemoveReviewReply;
using ZIONShop.Reviews.Application.Features.ReplyToReview;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class ReplyRemoveReviewReplyCommandHandlerTests
{
    private static (ReviewsDbContext db, ReviewRepository reviews) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ReviewsDbContext(options);
        var reviews = new ReviewRepository(db);
        return (db, reviews);
    }

    [Fact]
    public async Task ReplyToReview_Should_SetReplyContent_When_ReviewExists()
    {
        var (db, reviews) = BuildScope();
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, null, "A five star review for this product.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await db.SaveChangesAsync();

        var handler = new ReplyToReviewCommandHandler(reviews, db);
        var result = await handler.Handle(new ReplyToReviewCommand(review.Id, "Thank you for the review!"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReplyContent.Should().Be("Thank you for the review!");
        result.Value.RepliedAt.Should().NotBeNull();

        var persisted = await db.Reviews.FirstAsync(r => r.Id == review.Id);
        persisted.ReplyContent.Should().Be("Thank you for the review!");
    }

    [Fact]
    public async Task ReplyToReview_Should_ReturnNotFound_When_ReviewDoesNotExist()
    {
        var (db, reviews) = BuildScope();
        var handler = new ReplyToReviewCommandHandler(reviews, db);

        var result = await handler.Handle(new ReplyToReviewCommand(Guid.NewGuid(), "Thanks"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ReviewNotFound");
    }

    [Fact]
    public async Task RemoveReviewReply_Should_ClearReply_When_ReplyExists()
    {
        var (db, reviews) = BuildScope();
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, null, "A five star review for this product.", "ng***", "Test Product");
        review.Reply("Thank you!");
        await db.Reviews.AddAsync(review);
        await db.SaveChangesAsync();

        var handler = new RemoveReviewReplyCommandHandler(reviews, db);
        var result = await handler.Handle(new RemoveReviewReplyCommand(review.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ReplyContent.Should().BeNull();

        var persisted = await db.Reviews.FirstAsync(r => r.Id == review.Id);
        persisted.ReplyContent.Should().BeNull();
    }

    [Fact]
    public async Task RemoveReviewReply_Should_ReturnConflict_When_NoReplyExists()
    {
        var (db, reviews) = BuildScope();
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, null, "A five star review for this product.", "ng***", "Test Product");
        await db.Reviews.AddAsync(review);
        await db.SaveChangesAsync();

        var handler = new RemoveReviewReplyCommandHandler(reviews, db);
        var result = await handler.Handle(new RemoveReviewReplyCommand(review.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.NoReply");
    }

    [Fact]
    public async Task RemoveReviewReply_Should_ReturnNotFound_When_ReviewDoesNotExist()
    {
        var (db, reviews) = BuildScope();
        var handler = new RemoveReviewReplyCommandHandler(reviews, db);

        var result = await handler.Handle(new RemoveReviewReplyCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ReviewNotFound");
    }
}

using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Domain.Enums;

namespace ZIONShop.Reviews.Tests.Domain;

public class ReviewTests
{
    private static Review CreateReview() =>
        Review.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, "Great", "Really great product, highly recommend it.", "ng***", "Test Product");

    [Fact]
    public void Create_Should_SetStatusPublished()
    {
        var review = CreateReview();

        review.Status.Should().Be(ReviewStatus.Published);
    }

    [Fact]
    public void Hide_Should_SetStatusHiddenAndReason()
    {
        var review = CreateReview();

        var result = review.Hide("Spam");

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Hidden);
        review.HiddenReason.Should().Be("Spam");
    }

    [Fact]
    public void Hide_Should_Fail_When_AlreadyHidden()
    {
        var review = CreateReview();
        review.Hide("Spam");

        var result = review.Hide("Spam again");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.AlreadyHidden");
    }

    [Fact]
    public void Show_Should_ClearHiddenReason()
    {
        var review = CreateReview();
        review.Hide("Spam");

        var result = review.Show();

        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        review.HiddenReason.Should().BeNull();
    }

    [Fact]
    public void Show_Should_Fail_When_AlreadyPublished()
    {
        var review = CreateReview();

        var result = review.Show();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.AlreadyPublished");
    }

    [Fact]
    public void UpdateContent_Should_ChangeRatingTitleContent()
    {
        var review = CreateReview();

        review.UpdateContent(3, "Updated title", "Updated content that is long enough.");

        review.Rating.Should().Be(3);
        review.Title.Should().Be("Updated title");
        review.Content.Should().Be("Updated content that is long enough.");
    }

    [Fact]
    public void Reply_Should_SetReplyContentAndRepliedAt()
    {
        var review = CreateReview();

        var result = review.Reply("  Thanks for the feedback!  ");

        result.IsSuccess.Should().BeTrue();
        review.ReplyContent.Should().Be("Thanks for the feedback!");
        review.RepliedAt.Should().NotBeNull();
    }

    [Fact]
    public void Reply_Should_OverwritePreviousReply_When_ReplyingTwice()
    {
        var review = CreateReview();
        review.Reply("First reply");

        var result = review.Reply("Second reply");

        result.IsSuccess.Should().BeTrue();
        review.ReplyContent.Should().Be("Second reply");
    }

    [Fact]
    public void Reply_Should_Fail_When_ContentIsEmpty()
    {
        var review = CreateReview();

        var result = review.Reply("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ReplyContentRequired");
    }

    [Fact]
    public void RemoveReply_Should_ClearReplyContentAndRepliedAt()
    {
        var review = CreateReview();
        review.Reply("Thanks!");

        var result = review.RemoveReply();

        result.IsSuccess.Should().BeTrue();
        review.ReplyContent.Should().BeNull();
        review.RepliedAt.Should().BeNull();
    }

    [Fact]
    public void RemoveReply_Should_Fail_When_NoReplyExists()
    {
        var review = CreateReview();

        var result = review.RemoveReply();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.NoReply");
    }
}

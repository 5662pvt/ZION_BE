using ZIONShop.Reviews.Application.Features.CreateReview;

namespace ZIONShop.Reviews.Tests.Validators;

public class CreateReviewCommandValidatorTests
{
    private readonly CreateReviewCommandValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Validate_Should_Fail_When_RatingOutOfRange(int rating)
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), rating, null, "A perfectly valid piece of content here.");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Fail_When_ContentTooShort()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null, "short");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Fail_When_ContentTooLong()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null, new string('a', 2001));

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Fail_When_TitleTooLong()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, new string('a', 121), "A perfectly valid piece of content here.");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Succeed_When_PayloadValid()
    {
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, "Great", "A perfectly valid piece of content here.");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Should_TrimBeforeMeasuringContentLength()
    {
        // 8 trimmed chars (< 10) padded with whitespace to clear a naive un-trimmed length check.
        var cmd = new CreateReviewCommand(Guid.NewGuid(), Guid.NewGuid(), 5, null, "   short   ");

        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }
}

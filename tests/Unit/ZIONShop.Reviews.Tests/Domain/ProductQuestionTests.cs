using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Tests.Domain;

public class ProductQuestionTests
{
    private static ProductQuestion CreateQuestion() =>
        ProductQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), "ng***", "Does this come in other colors?");

    [Fact]
    public void Create_Should_HaveNoAnswer()
    {
        var question = CreateQuestion();

        question.AnswerContent.Should().BeNull();
        question.AnsweredAt.Should().BeNull();
        question.AnsweredByUserId.Should().BeNull();
    }

    [Fact]
    public void Answer_Should_SetAnswerContentAndAnsweredBy()
    {
        var question = CreateQuestion();
        var staffId = Guid.NewGuid();

        var result = question.Answer(staffId, "  Yes, blue and black.  ");

        result.IsSuccess.Should().BeTrue();
        question.AnswerContent.Should().Be("Yes, blue and black.");
        question.AnsweredAt.Should().NotBeNull();
        question.AnsweredByUserId.Should().Be(staffId);
    }

    [Fact]
    public void Answer_Should_OverwritePreviousAnswer_When_AnsweringTwice()
    {
        var question = CreateQuestion();
        question.Answer(Guid.NewGuid(), "First answer");

        var secondStaffId = Guid.NewGuid();
        var result = question.Answer(secondStaffId, "Second answer");

        result.IsSuccess.Should().BeTrue();
        question.AnswerContent.Should().Be("Second answer");
        question.AnsweredByUserId.Should().Be(secondStaffId);
    }

    [Fact]
    public void Answer_Should_Fail_When_ContentIsEmpty()
    {
        var question = CreateQuestion();

        var result = question.Answer(Guid.NewGuid(), "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.AnswerContentRequired");
    }
}

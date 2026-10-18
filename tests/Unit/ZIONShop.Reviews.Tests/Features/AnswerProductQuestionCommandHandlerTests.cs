using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.AnswerProductQuestion;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class AnswerProductQuestionCommandHandlerTests
{
    private static (ReviewsDbContext db, ProductQuestionRepository questions) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ReviewsDbContext(options);
        return (db, new ProductQuestionRepository(db));
    }

    [Fact]
    public async Task Handle_Should_SetAnswer_When_QuestionExists()
    {
        var (db, questions) = BuildScope();
        var question = ProductQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), "ng***", "Does this come in other colors?");
        await db.ProductQuestions.AddAsync(question);
        await db.SaveChangesAsync();

        var staffId = Guid.NewGuid();
        var handler = new AnswerProductQuestionCommandHandler(questions, db);
        var result = await handler.Handle(new AnswerProductQuestionCommand(question.Id, staffId, "Yes, blue and black."), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AnswerContent.Should().Be("Yes, blue and black.");

        var persisted = await db.ProductQuestions.FirstAsync(q => q.Id == question.Id);
        persisted.AnswerContent.Should().Be("Yes, blue and black.");
        persisted.AnsweredByUserId.Should().Be(staffId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_QuestionDoesNotExist()
    {
        var (db, questions) = BuildScope();
        var handler = new AnswerProductQuestionCommandHandler(questions, db);

        var result = await handler.Handle(new AnswerProductQuestionCommand(Guid.NewGuid(), Guid.NewGuid(), "Yes"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.QuestionNotFound");
    }
}

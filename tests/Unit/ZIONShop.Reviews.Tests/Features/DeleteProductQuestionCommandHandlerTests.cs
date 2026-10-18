using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.DeleteProductQuestion;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class DeleteProductQuestionCommandHandlerTests
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
    public async Task Handle_Should_RemoveQuestion_When_RequestedByOwner()
    {
        var (db, questions) = BuildScope();
        var userId = Guid.NewGuid();
        var question = ProductQuestion.Create(Guid.NewGuid(), userId, "ng***", "Does this come in other colors?");
        await db.ProductQuestions.AddAsync(question);
        await db.SaveChangesAsync();

        var handler = new DeleteProductQuestionCommandHandler(questions, db);
        var result = await handler.Handle(new DeleteProductQuestionCommand(question.Id, userId, IsStaffOrAdmin: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.ProductQuestions.AnyAsync(q => q.Id == question.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_RemoveQuestion_When_RequestedByAdmin()
    {
        var (db, questions) = BuildScope();
        var question = ProductQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), "ng***", "Does this come in other colors?");
        await db.ProductQuestions.AddAsync(question);
        await db.SaveChangesAsync();

        var handler = new DeleteProductQuestionCommandHandler(questions, db);
        var result = await handler.Handle(new DeleteProductQuestionCommand(question.Id, Guid.NewGuid(), IsStaffOrAdmin: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.ProductQuestions.AnyAsync(q => q.Id == question.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_When_RequestedByDifferentNonStaffUser()
    {
        var (db, questions) = BuildScope();
        var question = ProductQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), "ng***", "Does this come in other colors?");
        await db.ProductQuestions.AddAsync(question);
        await db.SaveChangesAsync();

        var handler = new DeleteProductQuestionCommandHandler(questions, db);
        var result = await handler.Handle(new DeleteProductQuestionCommand(question.Id, Guid.NewGuid(), IsStaffOrAdmin: false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.NotQuestionOwner");
        (await db.ProductQuestions.AnyAsync(q => q.Id == question.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_QuestionDoesNotExist()
    {
        var (db, questions) = BuildScope();
        var handler = new DeleteProductQuestionCommandHandler(questions, db);

        var result = await handler.Handle(new DeleteProductQuestionCommand(Guid.NewGuid(), Guid.NewGuid(), IsStaffOrAdmin: false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.QuestionNotFound");
    }
}

using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.Features.GetProductQuestions;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class GetProductQuestionsQueryHandlerTests
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
    public async Task Handle_Should_ReturnOnlyQuestionsForProduct()
    {
        var (db, questions) = BuildScope();
        var productId = Guid.NewGuid();
        var q1 = ProductQuestion.Create(productId, Guid.NewGuid(), "ng***", "Question for product A");
        var q2 = ProductQuestion.Create(Guid.NewGuid(), Guid.NewGuid(), "ab***", "Question for a different product");
        await db.ProductQuestions.AddRangeAsync(q1, q2);
        await db.SaveChangesAsync();

        var handler = new GetProductQuestionsQueryHandler(questions);
        var result = await handler.Handle(new GetProductQuestionsQuery(productId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(q => q.Id == q1.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_IncludeAnswer_When_QuestionHasBeenAnswered()
    {
        var (db, questions) = BuildScope();
        var productId = Guid.NewGuid();
        var question = ProductQuestion.Create(productId, Guid.NewGuid(), "ng***", "Does this come in other colors?");
        question.Answer(Guid.NewGuid(), "Yes, blue and black.");
        await db.ProductQuestions.AddAsync(question);
        await db.SaveChangesAsync();

        var handler = new GetProductQuestionsQueryHandler(questions);
        var result = await handler.Handle(new GetProductQuestionsQuery(productId, 1, 20), CancellationToken.None);

        result.Value.Items.Should().ContainSingle(q => q.AnswerContent == "Yes, blue and black.");
    }
}

using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Products;
using ZIONShop.Reviews.Application.Features.AskProductQuestion;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class AskProductQuestionCommandHandlerTests
{
    private static readonly ProductCatalogItem PublishedProduct = new(Guid.NewGuid(), "SKU-1", "Test Product", "Published", null);

    private static (ReviewsDbContext db, ProductQuestionRepository questions) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ReviewsDbContext(options);
        return (db, new ProductQuestionRepository(db));
    }

    private static AskProductQuestionCommandHandler BuildHandler(
        ReviewsDbContext db,
        ProductQuestionRepository questions,
        ProductCatalogItem? product,
        UserContact? contact = null)
    {
        var contactReader = new Mock<IUserContactReader>();
        contactReader.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact ?? new UserContact(Guid.NewGuid(), "nguyen@example.com", null));

        var catalogReader = new Mock<IProductCatalogReader>();
        catalogReader.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        return new AskProductQuestionCommandHandler(questions, db, contactReader.Object, catalogReader.Object);
    }

    [Fact]
    public async Task Handle_Should_CreateQuestion_When_ProductIsPublished()
    {
        var (db, questions) = BuildScope();
        var handler = BuildHandler(db, questions, PublishedProduct);

        var cmd = new AskProductQuestionCommand(Guid.NewGuid(), PublishedProduct.ProductId, "Does this come in other colors?");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Does this come in other colors?");
        result.Value.AuthorName.Should().Be("ng***");

        var persisted = await db.ProductQuestions.FirstAsync(q => q.Id == result.Value.Id);
        persisted.ProductId.Should().Be(PublishedProduct.ProductId);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ProductIsNotPublished()
    {
        var (db, questions) = BuildScope();
        var draftProduct = PublishedProduct with { Status = "Draft" };
        var handler = BuildHandler(db, questions, draftProduct);

        var cmd = new AskProductQuestionCommand(Guid.NewGuid(), PublishedProduct.ProductId, "Does this come in other colors?");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ProductNotAvailable");
    }

    [Fact]
    public async Task Handle_Should_UseDisplayName_When_ContactHasDisplayName()
    {
        var (db, questions) = BuildScope();
        var contact = new UserContact(Guid.NewGuid(), "someone@example.com", "Nguyen Van A");
        var handler = BuildHandler(db, questions, PublishedProduct, contact);

        var cmd = new AskProductQuestionCommand(Guid.NewGuid(), PublishedProduct.ProductId, "Does this come in other colors?");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Value.AuthorName.Should().Be("Nguyen Van A");
    }
}

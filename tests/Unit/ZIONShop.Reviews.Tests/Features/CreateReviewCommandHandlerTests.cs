using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Reviews;
using ZIONShop.Reviews.Application.Features.CreateReview;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;
using ZIONShop.Reviews.Tests.Fakes;

namespace ZIONShop.Reviews.Tests.Features;

public class CreateReviewCommandHandlerTests
{
    private static readonly ProductCatalogItem PublishedProduct = new(Guid.NewGuid(), "SKU-1", "Test Product", "Published", null);

    private static (ReviewsDbContext db, ReviewRepository reviews, ProductRatingSummaryUpdater updater) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ReviewsDbContext(options);
        var reviews = new ReviewRepository(db);
        var summaries = new ProductRatingSummaryRepository(db);
        var updater = new ProductRatingSummaryUpdater(reviews, summaries, db);
        return (db, reviews, updater);
    }

    private static CreateReviewCommandHandler BuildHandler(
        ReviewsDbContext db,
        ReviewRepository reviews,
        ProductRatingSummaryUpdater updater,
        Guid? completedOrderId,
        ProductCatalogItem? product,
        UserContact? contact = null)
    {
        var purchaseVerifier = new Mock<IPurchaseVerifier>();
        purchaseVerifier.Setup(x => x.GetCompletedOrderIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedOrderId);

        var contactReader = new Mock<IUserContactReader>();
        contactReader.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact ?? new UserContact(Guid.NewGuid(), "nguyen@example.com", null));

        var catalogReader = new Mock<IProductCatalogReader>();
        catalogReader.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        return new CreateReviewCommandHandler(reviews, db, updater, purchaseVerifier.Object, contactReader.Object, catalogReader.Object);
    }

    [Fact]
    public async Task Handle_Should_CreateReview_When_UserPurchasedProduct()
    {
        var (db, reviews, updater) = BuildScope();
        var orderId = Guid.NewGuid();
        var handler = BuildHandler(db, reviews, updater, orderId, PublishedProduct);

        var cmd = new CreateReviewCommand(Guid.NewGuid(), PublishedProduct.ProductId, 5, "Great", "Really great product, highly recommend it.");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(5);
        result.Value.AuthorName.Should().Be("ng***");

        var summary = await db.ProductRatingSummaries.FirstAsync(s => s.ProductId == PublishedProduct.ProductId);
        summary.Count.Should().Be(1);
        summary.Average.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_Should_ReturnForbidden_When_UserHasNotPurchasedProduct()
    {
        var (db, reviews, updater) = BuildScope();
        var handler = BuildHandler(db, reviews, updater, completedOrderId: null, PublishedProduct);

        var cmd = new CreateReviewCommand(Guid.NewGuid(), PublishedProduct.ProductId, 5, null, "Really great product, highly recommend it.");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.NotEligible");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_UserAlreadyReviewedProduct()
    {
        var (db, reviews, updater) = BuildScope();
        var userId = Guid.NewGuid();
        var existing = Review.Create(PublishedProduct.ProductId, userId, Guid.NewGuid(), 4, null, "An earlier review, already submitted once.", "ng***", "Test Product");
        await db.Reviews.AddAsync(existing);
        await db.SaveChangesAsync();

        var handler = BuildHandler(db, reviews, updater, Guid.NewGuid(), PublishedProduct);
        var cmd = new CreateReviewCommand(userId, PublishedProduct.ProductId, 5, null, "Really great product, highly recommend it.");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.AlreadyReviewed");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ProductIsNotPublished()
    {
        var (db, reviews, updater) = BuildScope();
        var draftProduct = PublishedProduct with { Status = "Draft" };
        var handler = BuildHandler(db, reviews, updater, Guid.NewGuid(), draftProduct);

        var cmd = new CreateReviewCommand(Guid.NewGuid(), PublishedProduct.ProductId, 5, null, "Really great product, highly recommend it.");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Reviews.ProductNotAvailable");
    }

    [Fact]
    public async Task Handle_Should_UseDisplayName_When_ContactHasDisplayName()
    {
        var (db, reviews, updater) = BuildScope();
        var contact = new UserContact(Guid.NewGuid(), "someone@example.com", "Nguyen Van A");
        var handler = BuildHandler(db, reviews, updater, Guid.NewGuid(), PublishedProduct, contact);

        var cmd = new CreateReviewCommand(Guid.NewGuid(), PublishedProduct.ProductId, 5, null, "Really great product, highly recommend it.");
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Value.AuthorName.Should().Be("Nguyen Van A");
    }

    [Fact]
    public async Task Handle_Should_AddExactlyOneReviewCreatedOutboxEvent_When_ReviewIsCreated()
    {
        var (db, reviews, updater) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var fakeUow = new FakeReviewsUnitOfWork();

        var purchaseVerifier = new Mock<IPurchaseVerifier>();
        purchaseVerifier.Setup(x => x.GetCompletedOrderIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderId);

        var contactReader = new Mock<IUserContactReader>();
        contactReader.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserContact(userId, "nguyen@example.com", null));

        var catalogReader = new Mock<IProductCatalogReader>();
        catalogReader.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PublishedProduct);

        var handler = new CreateReviewCommandHandler(reviews, fakeUow, updater, purchaseVerifier.Object, contactReader.Object, catalogReader.Object);
        var cmd = new CreateReviewCommand(userId, PublishedProduct.ProductId, 5, "Great", "Really great product, highly recommend it.");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fakeUow.OutboxEvents.Should().ContainSingle();
        var evt = fakeUow.OutboxEvents.Single().Should().BeOfType<ReviewCreatedIntegrationEvent>().Subject;
        evt.ReviewId.Should().Be(result.Value.Id);
        evt.UserId.Should().Be(userId);
        evt.ProductId.Should().Be(PublishedProduct.ProductId);
        evt.OrderId.Should().Be(orderId);
        evt.Rating.Should().Be(5);
    }
}

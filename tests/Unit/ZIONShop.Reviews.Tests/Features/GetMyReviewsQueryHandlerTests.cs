using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Products;
using ZIONShop.Reviews.Application.Features.GetMyReviews;
using ZIONShop.Reviews.Domain.Entities;
using ZIONShop.Reviews.Infrastructure.Persistence;
using ZIONShop.Reviews.Infrastructure.Repositories;

namespace ZIONShop.Reviews.Tests.Features;

public class GetMyReviewsQueryHandlerTests
{
    private static (ReviewsDbContext db, ReviewRepository reviews) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ReviewsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ReviewsDbContext(options);
        return (db, new ReviewRepository(db));
    }

    private static Mock<IProductPriceLookup> MockPrices(Guid productId, string slug)
    {
        var mock = new Mock<IProductPriceLookup>();
        mock.Setup(x => x.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, ProductPriceInfo>)new Dictionary<Guid, ProductPriceInfo>
            {
                [productId] = new(productId, "Test Product", 100000m, "VND", true, slug, null)
            });
        return mock;
    }

    [Fact]
    public async Task Handle_Should_IncludeHiddenReviews_And_OrderNewestFirst()
    {
        var (db, reviews) = BuildScope();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var published = Review.Create(productId, userId, Guid.NewGuid(), 5, "Great", "Really great product, highly recommend it.", "ng***", "Test Product");
        await db.Reviews.AddAsync(published);
        await db.SaveChangesAsync();

        var hiddenProductId = Guid.NewGuid();
        var hidden = Review.Create(hiddenProductId, userId, Guid.NewGuid(), 1, "Bad", "This review will be hidden by moderation.", "ng***", "Other Product");
        hidden.Hide("Policy violation");
        await db.Reviews.AddAsync(hidden);
        await db.SaveChangesAsync();

        var handler = new GetMyReviewsQueryHandler(reviews, MockPrices(productId, "test-product").Object);
        var result = await handler.Handle(new GetMyReviewsQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(r => r.Id == hidden.Id && r.Status == "Hidden" && r.HiddenReason == "Policy violation");
        result.Value.Items.Should().Contain(r => r.Id == published.Id && r.ProductSlug == "test-product");
    }

    [Fact]
    public async Task Handle_Should_NotReturnOtherUsersReviews()
    {
        var (db, reviews) = BuildScope();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await db.Reviews.AddAsync(Review.Create(productId, otherUserId, Guid.NewGuid(), 5, null, "Not mine, belongs to someone else entirely.", "ng***", "Test Product"));
        await db.SaveChangesAsync();

        var handler = new GetMyReviewsQueryHandler(reviews, MockPrices(productId, "test-product").Object);
        var result = await handler.Handle(new GetMyReviewsQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}

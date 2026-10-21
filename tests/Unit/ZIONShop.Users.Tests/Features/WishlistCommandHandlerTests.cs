using Microsoft.EntityFrameworkCore;
using Moq;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Products;
using ZIONShop.Users.Application.Features.AddToWishlist;
using ZIONShop.Users.Application.Features.GetMyWishlist;
using ZIONShop.Users.Application.Features.GetMyWishlistIds;
using ZIONShop.Users.Application.Features.RemoveFromWishlist;
using ZIONShop.Users.Domain.Entities;
using ZIONShop.Users.Infrastructure.Persistence;
using ZIONShop.Users.Infrastructure.Repositories;

namespace ZIONShop.Users.Tests.Features;

public class WishlistCommandHandlerTests
{
    private static readonly ProductSummaryInfo PublishedProduct =
        new(Guid.NewGuid(), "Test Product", "test-product", 100000m, "VND", "http://img/test.png", true, 100000m, 1);

    private static (UsersDbContext db, WishlistRepository wishlist) BuildScope()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new UsersDbContext(options);
        return (db, new WishlistRepository(db));
    }

    private static Mock<IProductPriceLookup> MockSummaries(ProductSummaryInfo? product)
    {
        var mock = new Mock<IProductPriceLookup>();
        mock.Setup(x => x.GetProductSummaryAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, ProductSummaryInfo>)(product is not null && ids.Contains(product.ProductId)
                    ? new Dictionary<Guid, ProductSummaryInfo> { [product.ProductId] = product }
                    : new Dictionary<Guid, ProductSummaryInfo>()));
        return mock;
    }

    [Fact]
    public async Task AddToWishlist_Should_AddItem_When_ProductIsPublished()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        var handler = new AddToWishlistCommandHandler(wishlist, MockSummaries(PublishedProduct).Object, db);

        var result = await handler.Handle(new AddToWishlistCommand(userId, PublishedProduct.ProductId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InWishlist.Should().BeTrue();
        (await db.WishlistItems.CountAsync(w => w.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task AddToWishlist_Should_AddItem_When_ProductHasMultipleActiveVariants()
    {
        // The regression this guards: AddToWishlist used to call IProductPriceLookup.GetAsync (variant-keyed),
        // which reports IsAvailable=false for a product with 0 or 2+ active variants, so adding a
        // multi-variant product to the wishlist always failed with ProductNotFound.
        var multiVariantProduct = new ProductSummaryInfo(Guid.NewGuid(), "Shirt", "shirt", 50000m, "VND", null, true, 60000m, 2);
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        var handler = new AddToWishlistCommandHandler(wishlist, MockSummaries(multiVariantProduct).Object, db);

        var result = await handler.Handle(new AddToWishlistCommand(userId, multiVariantProduct.ProductId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InWishlist.Should().BeTrue();
    }

    [Fact]
    public async Task AddToWishlist_Should_BeIdempotent_When_AlreadyInWishlist()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        await db.WishlistItems.AddAsync(WishlistItem.Create(userId, PublishedProduct.ProductId));
        await db.SaveChangesAsync();

        var handler = new AddToWishlistCommandHandler(wishlist, MockSummaries(PublishedProduct).Object, db);
        var result = await handler.Handle(new AddToWishlistCommand(userId, PublishedProduct.ProductId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.WishlistItems.CountAsync(w => w.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task AddToWishlist_Should_ReturnNotFound_When_ProductIsNotPublished()
    {
        var (db, wishlist) = BuildScope();
        var handler = new AddToWishlistCommandHandler(wishlist, MockSummaries(null).Object, db);

        var result = await handler.Handle(new AddToWishlistCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Users.ProductNotFound");
    }

    [Fact]
    public async Task AddToWishlist_Should_ReturnWishlistFull_When_UserAlreadyHas200Items()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        for (var i = 0; i < 200; i++)
            await db.WishlistItems.AddAsync(WishlistItem.Create(userId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var handler = new AddToWishlistCommandHandler(wishlist, MockSummaries(PublishedProduct).Object, db);
        var result = await handler.Handle(new AddToWishlistCommand(userId, PublishedProduct.ProductId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Users.WishlistFull");
    }

    [Fact]
    public async Task RemoveFromWishlist_Should_RemoveItem_When_ItExists()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        await db.WishlistItems.AddAsync(WishlistItem.Create(userId, PublishedProduct.ProductId));
        await db.SaveChangesAsync();

        var handler = new RemoveFromWishlistCommandHandler(wishlist, db);
        var result = await handler.Handle(new RemoveFromWishlistCommand(userId, PublishedProduct.ProductId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InWishlist.Should().BeFalse();
        (await db.WishlistItems.CountAsync(w => w.UserId == userId)).Should().Be(0);
    }

    [Fact]
    public async Task RemoveFromWishlist_Should_BeIdempotent_When_ItemDoesNotExist()
    {
        var (db, wishlist) = BuildScope();
        var handler = new RemoveFromWishlistCommandHandler(wishlist, db);

        var result = await handler.Handle(new RemoveFromWishlistCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InWishlist.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyWishlist_Should_ReturnNewestFirst_And_SkipUnpublishedProducts()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        var unpublishedProductId = Guid.NewGuid();

        var first = WishlistItem.Create(userId, PublishedProduct.ProductId);
        await db.WishlistItems.AddAsync(first);
        await db.SaveChangesAsync();

        await Task.Delay(10);
        var second = WishlistItem.Create(userId, unpublishedProductId);
        await db.WishlistItems.AddAsync(second);
        await db.SaveChangesAsync();

        var summariesMock = MockSummaries(PublishedProduct);
        var stockMock = new Mock<IProductStockSummaryReader>();
        stockMock.Setup(x => x.GetAvailableByProductAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [PublishedProduct.ProductId] = 5 });

        var handler = new GetMyWishlistQueryHandler(wishlist, summariesMock.Object, stockMock.Object);
        var result = await handler.Handle(new GetMyWishlistQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().ProductId.Should().Be(PublishedProduct.ProductId);
        result.Value.Items.Single().InStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyWishlist_Should_ShowMultiVariantProduct_With_MinPriceAndVariantCount()
    {
        // The regression this guards: the list view used to call IStockAvailabilityReader.GetAvailableAsync
        // (variant-keyed) with a PRODUCT id, which never matches a multi-variant product's inventory rows
        // (keyed by variant id), so it always showed InStock=false; and priced via GetManyAsync, which marked
        // the item unavailable and silently dropped it from the page.
        var multiVariantProduct = new ProductSummaryInfo(Guid.NewGuid(), "Shirt", "shirt", 50000m, "VND", null, true, 70000m, 3);
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        await db.WishlistItems.AddAsync(WishlistItem.Create(userId, multiVariantProduct.ProductId));
        await db.SaveChangesAsync();

        var summariesMock = MockSummaries(multiVariantProduct);
        var stockMock = new Mock<IProductStockSummaryReader>();
        stockMock.Setup(x => x.GetAvailableByProductAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [multiVariantProduct.ProductId] = 12 });

        var handler = new GetMyWishlistQueryHandler(wishlist, summariesMock.Object, stockMock.Object);
        var result = await handler.Handle(new GetMyWishlistQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.InStock.Should().BeTrue();
        item.Price.Should().Be(50000m);
        item.MinPrice.Should().Be(50000m);
        item.MaxPrice.Should().Be(70000m);
        item.VariantCount.Should().Be(3);
    }

    [Fact]
    public async Task GetMyWishlist_Should_FilterBeforePaging_When_MiddleItemIsUnpublished()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();

        var oldest = new ProductSummaryInfo(Guid.NewGuid(), "Oldest", "oldest", 10000m, "VND", null, true, 10000m, 1);
        var middleUnpublished = new ProductSummaryInfo(Guid.NewGuid(), "Middle", "middle", 20000m, "VND", null, false, 20000m, 1);
        var newest = new ProductSummaryInfo(Guid.NewGuid(), "Newest", "newest", 30000m, "VND", null, true, 30000m, 1);

        foreach (var product in new[] { oldest, middleUnpublished, newest })
        {
            await db.WishlistItems.AddAsync(WishlistItem.Create(userId, product.ProductId));
            await db.SaveChangesAsync();
            await Task.Delay(10);
        }

        var summaryMap = new Dictionary<Guid, ProductSummaryInfo>
        {
            [oldest.ProductId] = oldest,
            [middleUnpublished.ProductId] = middleUnpublished,
            [newest.ProductId] = newest,
        };
        var summariesMock = new Mock<IProductPriceLookup>();
        summariesMock.Setup(x => x.GetProductSummaryAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, ProductSummaryInfo>)summaryMap);

        var stockMock = new Mock<IProductStockSummaryReader>();
        stockMock.Setup(x => x.GetAvailableByProductAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [oldest.ProductId] = 1, [newest.ProductId] = 1 });

        var handler = new GetMyWishlistQueryHandler(wishlist, summariesMock.Object, stockMock.Object);
        var result = await handler.Handle(new GetMyWishlistQuery(userId, 1, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Select(i => i.ProductId).Should().Equal(newest.ProductId, oldest.ProductId);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetMyWishlistIds_Should_ReturnOnlyCurrentUsersProductIds()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await db.WishlistItems.AddAsync(WishlistItem.Create(userId, productId));
        await db.WishlistItems.AddAsync(WishlistItem.Create(otherUserId, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var handler = new GetMyWishlistIdsQueryHandler(wishlist);
        var result = await handler.Handle(new GetMyWishlistIdsQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(productId);
    }
}

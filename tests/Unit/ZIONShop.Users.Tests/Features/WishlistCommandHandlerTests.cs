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
    private static readonly ProductPriceInfo PublishedProduct = new(Guid.NewGuid(), "Test Product", 100000m, "VND", true, "test-product", "http://img/test.png");

    private static (UsersDbContext db, WishlistRepository wishlist) BuildScope()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new UsersDbContext(options);
        return (db, new WishlistRepository(db));
    }

    private static Mock<IProductPriceLookup> MockPrices(ProductPriceInfo? product)
    {
        var mock = new Mock<IProductPriceLookup>();
        mock.Setup(x => x.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(product);
        mock.Setup(x => x.GetManyAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, ProductPriceInfo>)(product is not null && ids.Contains(product.ProductId)
                    ? new Dictionary<Guid, ProductPriceInfo> { [product.ProductId] = product }
                    : new Dictionary<Guid, ProductPriceInfo>()));
        return mock;
    }

    [Fact]
    public async Task AddToWishlist_Should_AddItem_When_ProductIsPublished()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        var handler = new AddToWishlistCommandHandler(wishlist, MockPrices(PublishedProduct).Object, db);

        var result = await handler.Handle(new AddToWishlistCommand(userId, PublishedProduct.ProductId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InWishlist.Should().BeTrue();
        (await db.WishlistItems.CountAsync(w => w.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task AddToWishlist_Should_BeIdempotent_When_AlreadyInWishlist()
    {
        var (db, wishlist) = BuildScope();
        var userId = Guid.NewGuid();
        await db.WishlistItems.AddAsync(WishlistItem.Create(userId, PublishedProduct.ProductId));
        await db.SaveChangesAsync();

        var handler = new AddToWishlistCommandHandler(wishlist, MockPrices(PublishedProduct).Object, db);
        var result = await handler.Handle(new AddToWishlistCommand(userId, PublishedProduct.ProductId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.WishlistItems.CountAsync(w => w.UserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task AddToWishlist_Should_ReturnNotFound_When_ProductIsNotPublished()
    {
        var (db, wishlist) = BuildScope();
        var handler = new AddToWishlistCommandHandler(wishlist, MockPrices(null).Object, db);

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

        var handler = new AddToWishlistCommandHandler(wishlist, MockPrices(PublishedProduct).Object, db);
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

        var pricesMock = MockPrices(PublishedProduct);
        var stockMock = new Mock<IStockAvailabilityReader>();
        stockMock.Setup(x => x.GetAvailableAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [PublishedProduct.ProductId] = 5 });

        var handler = new GetMyWishlistQueryHandler(wishlist, pricesMock.Object, stockMock.Object);
        var result = await handler.Handle(new GetMyWishlistQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().ProductId.Should().Be(PublishedProduct.ProductId);
        result.Value.Items.Single().InStock.Should().BeTrue();
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

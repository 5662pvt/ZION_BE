using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.GetCart;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;

namespace ZIONShop.Cart.Tests.Features;

public class GetCartQueryHandlerTests
{
    private static (CartDbContext db, CartRepository carts) BuildScope()
    {
        var options = new DbContextOptionsBuilder<CartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new CartDbContext(options);
        return (db, new CartRepository(db));
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyCart_When_UserHasNoCart()
    {
        var (db, carts) = BuildScope();
        var handler = new GetCartQueryHandler(carts, db);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new GetCartQuery(userId, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_CreateGuestCart_When_AnonymousIdHasNoCart()
    {
        var (db, carts) = BuildScope();
        var handler = new GetCartQueryHandler(carts, db);
        var anonymousId = Guid.NewGuid().ToString("N");

        var result = await handler.Handle(new GetCartQuery(null, anonymousId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AnonymousId.Should().Be(anonymousId);
        result.Value.Items.Should().BeEmpty();

        var persisted = await carts.GetByAnonymousIdAsync(anonymousId, CancellationToken.None);
        persisted.Should().NotBeNull();
    }
}

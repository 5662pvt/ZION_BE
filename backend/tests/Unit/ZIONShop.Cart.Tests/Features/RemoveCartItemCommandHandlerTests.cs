using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.RemoveItem;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;
using ZIONShop.Cart.Tests.Fakes;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Tests.Features;

public class RemoveCartItemCommandHandlerTests
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
    public async Task Handle_Should_RemoveItem_When_ItemExists()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var cart = DomainCart.CreateForUser(userId);
        var item = cart.AddItem(Guid.NewGuid(), "Widget", 1, 10m, "USD");
        await carts.AddAsync(cart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RemoveCartItemCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new RemoveCartItemCommand(userId, null, item.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ItemNotFound()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var cart = DomainCart.CreateForUser(userId);
        await carts.AddAsync(cart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RemoveCartItemCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new RemoveCartItemCommand(userId, null, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ItemNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_CartNotFound()
    {
        var (db, carts) = BuildScope();
        var handler = new RemoveCartItemCommandHandler(carts, db, new FakeProductPriceLookup());

        var result = await handler.Handle(new RemoveCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.CartNotFound");
    }
}

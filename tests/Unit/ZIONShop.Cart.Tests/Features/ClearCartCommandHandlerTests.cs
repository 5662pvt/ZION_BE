using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.ClearCart;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;
using ZIONShop.Cart.Tests.Fakes;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Tests.Features;

public class ClearCartCommandHandlerTests
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
    public async Task Handle_Should_ClearItems_When_CartExists()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var cart = DomainCart.CreateForUser(userId);
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Widget", 1, 10m, "USD");
        cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Gadget", 2, 20m, "USD");
        await carts.AddAsync(cart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new ClearCartCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new ClearCartCommand(userId, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_CartNotFound()
    {
        var (db, carts) = BuildScope();
        var handler = new ClearCartCommandHandler(carts, db, new FakeProductPriceLookup());

        var result = await handler.Handle(new ClearCartCommand(Guid.NewGuid(), null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.CartNotFound");
    }
}

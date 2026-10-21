using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.UpdateItem;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;
using ZIONShop.Cart.Tests.Fakes;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Tests.Features;

public class UpdateCartItemCommandHandlerTests
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
    public async Task Handle_Should_UpdateQuantity_When_ItemExists()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var cart = DomainCart.CreateForUser(userId);
        var item = cart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Widget", 1, 10m, "USD");
        await carts.AddAsync(cart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateCartItemCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new UpdateCartItemCommand(userId, null, item.Id, 5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Id == item.Id && i.Quantity == 5);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ItemNotFound()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var cart = DomainCart.CreateForUser(userId);
        await carts.AddAsync(cart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new UpdateCartItemCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new UpdateCartItemCommand(userId, null, Guid.NewGuid(), 5), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ItemNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_CartNotFound()
    {
        var (db, carts) = BuildScope();
        var handler = new UpdateCartItemCommandHandler(carts, db, new FakeProductPriceLookup());

        var result = await handler.Handle(new UpdateCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 5), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.CartNotFound");
    }
}

public class UpdateCartItemCommandValidatorTests
{
    [Fact]
    public void Validator_Should_Fail_When_QuantityIsZero()
    {
        var validator = new UpdateCartItemCommandValidator();
        var result = validator.Validate(new UpdateCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 0));

        result.IsValid.Should().BeFalse();
    }
}

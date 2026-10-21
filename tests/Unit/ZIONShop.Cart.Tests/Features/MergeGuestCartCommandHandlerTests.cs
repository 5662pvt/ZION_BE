using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.MergeGuestCart;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;
using ZIONShop.Cart.Tests.Fakes;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Tests.Features;

public class MergeGuestCartCommandHandlerTests
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
    public async Task Handle_Should_AssignGuestCartToUser_When_UserHasNoCart()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var anonymousId = Guid.NewGuid().ToString("N");
        var guestCart = DomainCart.CreateForGuest(anonymousId);
        guestCart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Widget", 2, 10m, "USD");
        await carts.AddAsync(guestCart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new MergeGuestCartCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new MergeGuestCartCommand(userId, anonymousId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(userId);
        result.Value.Items.Should().ContainSingle(i => i.Quantity == 2);

        var mergedCart = await carts.GetByUserIdAsync(userId, CancellationToken.None);
        mergedCart!.AnonymousId.Should().BeNull();
        mergedCart.Items.Should().ContainSingle(i => i.Quantity == 2);

        var guestLookup = await carts.GetByAnonymousIdAsync(anonymousId, CancellationToken.None);
        guestLookup.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_MergeAndSumQuantities_When_UserCartAlreadyHasItems()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var sharedProductId = Guid.NewGuid();
        var sharedVariantId = Guid.NewGuid();
        var anonymousId = Guid.NewGuid().ToString("N");

        var userCart = DomainCart.CreateForUser(userId);
        userCart.AddItem(sharedProductId, sharedVariantId, "Widget", 1, 10m, "USD");
        await carts.AddAsync(userCart, CancellationToken.None);

        var guestCart = DomainCart.CreateForGuest(anonymousId);
        guestCart.AddItem(sharedProductId, sharedVariantId, "Widget", 2, 10m, "USD");
        guestCart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Gadget", 1, 20m, "USD");
        await carts.AddAsync(guestCart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new MergeGuestCartCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new MergeGuestCartCommand(userId, anonymousId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().ContainSingle(i => i.ProductId == sharedProductId && i.Quantity == 3);
        result.Value.TotalQuantity.Should().Be(4);

        var removedGuestCart = await carts.GetByAnonymousIdAsync(anonymousId, CancellationToken.None);
        removedGuestCart.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_ReturnUserCart_When_NoAnonymousIdProvided()
    {
        var (db, carts) = BuildScope();
        var userId = Guid.NewGuid();
        var userCart = DomainCart.CreateForUser(userId);
        userCart.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Widget", 1, 10m, "USD");
        await carts.AddAsync(userCart, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new MergeGuestCartCommandHandler(carts, db, new FakeProductPriceLookup());
        var result = await handler.Handle(new MergeGuestCartCommand(userId, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
    }
}

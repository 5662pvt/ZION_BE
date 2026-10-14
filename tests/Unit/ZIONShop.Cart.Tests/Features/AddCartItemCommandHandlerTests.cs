using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.AddItem;
using ZIONShop.Cart.Infrastructure.Persistence;
using ZIONShop.Cart.Infrastructure.Repositories;
using ZIONShop.Cart.Tests.Fakes;

namespace ZIONShop.Cart.Tests.Features;

public class AddCartItemCommandHandlerTests
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
    public async Task Handle_Should_AddNewLine_When_ProductAvailable()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 10m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Quantity.Should().Be(2);
        result.Value.Items[0].UnitPrice.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_Should_EnrichItem_With_ProductSlugAndImageUrl()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 10m, slug: "widget", primaryImageUrl: "https://cdn/widget.png");
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ProductSlug.Should().Be("widget");
        result.Value.Items[0].ImageUrl.Should().Be("https://cdn/widget.png");
    }

    [Fact]
    public async Task Handle_Should_IncrementQuantity_When_SameProductAddedAgain()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 10m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        await handler.Handle(new AddCartItemCommand(userId, null, productId, 2), CancellationToken.None);
        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, 3), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Quantity.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Should_AddSecondLine_When_DifferentProductAddedToExistingCart()
    {
        var (db, carts) = BuildScope();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var prices = new FakeProductPriceLookup()
            .Add(productA, "Widget", 10m)
            .Add(productB, "Gadget", 20m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        await handler.Handle(new AddCartItemCommand(userId, null, productA, 1), CancellationToken.None);
        var result = await handler.Handle(new AddCartItemCommand(userId, null, productB, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ProductUnavailable()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 10m, isAvailable: false);
        var handler = new AddCartItemCommandHandler(carts, db, prices);

        var result = await handler.Handle(new AddCartItemCommand(Guid.NewGuid(), null, productId, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ProductUnavailable");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ProductUnknown()
    {
        var (db, carts) = BuildScope();
        var prices = new FakeProductPriceLookup();
        var handler = new AddCartItemCommandHandler(carts, db, prices);

        var result = await handler.Handle(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ProductUnavailable");
    }
}

public class AddCartItemCommandValidatorTests
{
    [Fact]
    public void Validator_Should_Fail_When_QuantityIsZero()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Fail_When_QuantityExceedsLimit()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 101));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Fail_When_NeitherUserNorAnonymousIdProvided()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(null, null, Guid.NewGuid(), 1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Succeed_When_PayloadValid()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), 1));

        result.IsValid.Should().BeTrue();
    }
}

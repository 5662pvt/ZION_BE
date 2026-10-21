using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Features.AddItem;
using ZIONShop.Cart.Domain.Exceptions;
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

        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, null, 2), CancellationToken.None);

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

        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, null, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].ProductSlug.Should().Be("widget");
        result.Value.Items[0].ImageUrl.Should().Be("https://cdn/widget.png");
    }

    [Fact]
    public async Task Handle_Should_UseVariantImageUrl_When_VariantHasOwnImage()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().AddVariant(productId, variantId, "Đỏ / M", "TEE-RED-M", 100m, primaryImageUrl: "https://cdn/primary.png", variantImageUrl: "https://cdn/red.png");
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new AddCartItemCommand(userId, null, null, variantId, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].ImageUrl.Should().Be("https://cdn/red.png");
    }

    [Fact]
    public async Task Handle_Should_FallBackToPrimaryImageUrl_When_VariantHasNoOwnImage()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().AddVariant(productId, variantId, "Đỏ / M", "TEE-RED-M2", 100m, primaryImageUrl: "https://cdn/primary.png");
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new AddCartItemCommand(userId, null, null, variantId, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items[0].ImageUrl.Should().Be("https://cdn/primary.png");
    }

    [Fact]
    public async Task Handle_Should_IncrementQuantity_When_SameProductAddedAgain()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 10m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        await handler.Handle(new AddCartItemCommand(userId, null, productId, null, 2), CancellationToken.None);
        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, null, 3), CancellationToken.None);

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

        await handler.Handle(new AddCartItemCommand(userId, null, productA, null, 1), CancellationToken.None);
        var result = await handler.Handle(new AddCartItemCommand(userId, null, productB, null, 1), CancellationToken.None);

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

        var result = await handler.Handle(new AddCartItemCommand(Guid.NewGuid(), null, productId, null, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ProductUnavailable");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_ProductUnknown()
    {
        var (db, carts) = BuildScope();
        var prices = new FakeProductPriceLookup();
        var handler = new AddCartItemCommandHandler(carts, db, prices);

        var result = await handler.Handle(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), null, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.ProductUnavailable");
    }

    [Fact]
    public async Task Handle_Should_AddTwoLines_When_TwoVariantsOfSameProductAdded()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var variantS = Guid.NewGuid();
        var variantM = Guid.NewGuid();
        var prices = new FakeProductPriceLookup()
            .AddVariant(productId, variantS, "S", "TEE-S", 100m)
            .AddVariant(productId, variantM, "M", "TEE-M", 100m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        await handler.Handle(new AddCartItemCommand(userId, null, null, variantS, 1), CancellationToken.None);
        var result = await handler.Handle(new AddCartItemCommand(userId, null, null, variantM, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items.Should().Contain(i => i.VariantId == variantS && i.VariantLabel == "S" && i.Sku == "TEE-S");
        result.Value.Items.Should().Contain(i => i.VariantId == variantM && i.VariantLabel == "M" && i.Sku == "TEE-M");
    }

    [Fact]
    public async Task Handle_Should_MergeQuantity_When_SameVariantAddedTwice()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var variantS = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().AddVariant(productId, variantS, "S", "TEE-S", 100m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        await handler.Handle(new AddCartItemCommand(userId, null, null, variantS, 1), CancellationToken.None);
        var result = await handler.Handle(new AddCartItemCommand(userId, null, null, variantS, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task Handle_Should_ResolveDefaultVariant_When_ProductIdOnlyUsed_ForSingleVariantProduct()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Widget", 10m);
        var handler = new AddCartItemCommandHandler(carts, db, prices);
        var userId = Guid.NewGuid();

        var result = await handler.Handle(new AddCartItemCommand(userId, null, productId, null, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.VariantId == productId && i.ProductId == productId);
    }

    [Fact]
    public async Task Handle_Should_ReturnProductUnavailable_WithChooseVariantMessage_When_MultiVariantProductIdUsedWithoutVariant()
    {
        var (db, carts) = BuildScope();
        var productId = Guid.NewGuid();
        var prices = new FakeProductPriceLookup().Add(productId, "Multi", 10m, isAvailable: false);
        var handler = new AddCartItemCommandHandler(carts, db, prices);

        var result = await handler.Handle(new AddCartItemCommand(Guid.NewGuid(), null, productId, null, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CartErrors.ProductUnavailable);
        result.Error.Message.Should().Be("Vui lòng chọn phân loại hàng");
    }
}

public class AddCartItemCommandValidatorTests
{
    [Fact]
    public void Validator_Should_Fail_When_QuantityIsZero()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), null, 0));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Fail_When_QuantityExceedsLimit()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), null, 101));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Fail_When_NeitherUserNorAnonymousIdProvided()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(null, null, Guid.NewGuid(), null, 1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Fail_When_NeitherProductIdNorVariantIdProvided()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, null, null, 1));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_Should_Succeed_When_PayloadValid()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, Guid.NewGuid(), null, 1));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_Should_Succeed_When_OnlyVariantIdProvided()
    {
        var validator = new AddCartItemCommandValidator();
        var result = validator.Validate(new AddCartItemCommand(Guid.NewGuid(), null, null, Guid.NewGuid(), 1));

        result.IsValid.Should().BeTrue();
    }
}

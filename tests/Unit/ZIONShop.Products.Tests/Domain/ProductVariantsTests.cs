using FluentAssertions;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;

namespace ZIONShop.Products.Tests.Domain;

public class ProductVariantsTests
{
    private static Product CreateLegacy(decimal price = 10m) =>
        Product.Create("Test", "test", Sku.Create("SKU-DOM1"), Money.Create(price, "USD"), null, null, null);

    [Fact]
    public void Create_Should_SetDefaultVariantId_EqualToProductId()
    {
        var product = CreateLegacy();

        product.Variants.Should().ContainSingle();
        product.Variants.Single().Id.Should().Be(product.Id);
        product.Variants.Single().IsActive.Should().BeTrue();
        product.MinPrice.Should().Be(10m);
        product.MaxPrice.Should().Be(10m);
    }

    [Fact]
    public void CreateDraft_Then_SetVariants_Should_AssignNewGuids_NotProductId()
    {
        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);

        var options = new[] { new ProductOptionInput("Màu sắc", new[] { "Đỏ", "Xanh" }) };
        var variants = new[]
        {
            new ProductVariantInput(null, "Đỏ", null, "SKU-RED", 100m, "VND", true, null),
            new ProductVariantInput(null, "Xanh", null, "SKU-BLUE", 110m, "VND", true, null)
        };

        var result = product.SetVariants(options, variants);

        result.IsSuccess.Should().BeTrue();
        product.Variants.Should().HaveCount(2);
        product.Variants.Should().OnlyContain(v => v.Id != product.Id);
        product.MinPrice.Should().Be(100m);
        product.MaxPrice.Should().Be(110m);
    }

    [Fact]
    public void SetVariants_Should_Fail_When_MoreThanTwoOptions()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[]
        {
            new ProductOptionInput("A", new[] { "1" }),
            new ProductOptionInput("B", new[] { "1" }),
            new ProductOptionInput("C", new[] { "1" })
        };
        var variants = new[] { new ProductVariantInput(null, "1", "1", "SKU-X", 10m, "USD", true, null) };

        var result = product.SetVariants(options, variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.TooManyOptions");
    }

    [Fact]
    public void SetVariants_Should_Fail_When_OptionNamesDuplicate()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[] { new ProductOptionInput("Size", new[] { "S" }), new ProductOptionInput("size", new[] { "M" }) };
        var variants = new[] { new ProductVariantInput(null, "S", "M", "SKU-X", 10m, "USD", true, null) };

        var result = product.SetVariants(options, variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.DuplicateOptionName");
    }

    [Fact]
    public void SetVariants_Should_Fail_When_VariantValueNotDeclaredInOption()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[] { new ProductOptionInput("Size", new[] { "S", "M" }) };
        var variants = new[] { new ProductVariantInput(null, "L", null, "SKU-X", 10m, "USD", true, null) };

        var result = product.SetVariants(options, variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.VariantValueNotInOption");
    }

    [Fact]
    public void SetVariants_Should_Fail_When_ActiveCombinationDuplicated()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[] { new ProductOptionInput("Size", new[] { "S", "M" }) };
        var variants = new[]
        {
            new ProductVariantInput(null, "S", null, "SKU-S1", 10m, "USD", true, null),
            new ProductVariantInput(null, "S", null, "SKU-S2", 11m, "USD", true, null)
        };

        var result = product.SetVariants(options, variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.DuplicateVariantCombination");
    }

    [Fact]
    public void SetVariants_Should_Fail_When_SkuDuplicatedWithinProduct()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[] { new ProductOptionInput("Size", new[] { "S", "M" }) };
        var variants = new[]
        {
            new ProductVariantInput(null, "S", null, "SKU-DUP", 10m, "USD", true, null),
            new ProductVariantInput(null, "M", null, "sku-dup", 11m, "USD", true, null)
        };

        var result = product.SetVariants(options, variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.DuplicateVariantSku");
    }

    [Fact]
    public void SetVariants_Should_Fail_When_NoActiveVariant()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var variants = new[] { new ProductVariantInput(null, null, null, "SKU-X", 10m, "USD", false, null) };

        var result = product.SetVariants(Array.Empty<ProductOptionInput>(), variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.NoActiveVariant");
    }

    [Fact]
    public void SetVariants_Should_Fail_When_ImageIdNotOwnedByProduct()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var variants = new[] { new ProductVariantInput(null, null, null, "SKU-X", 10m, "USD", true, Guid.NewGuid()) };

        var result = product.SetVariants(Array.Empty<ProductOptionInput>(), variants);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.VariantImageMismatch");
    }

    [Fact]
    public void SetVariants_Should_KeepId_When_VariantMatchedById_And_DeactivateOmittedVariants()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[] { new ProductOptionInput("Size", new[] { "S", "M" }) };
        var initial = new[]
        {
            new ProductVariantInput(null, "S", null, "SKU-S", 10m, "USD", true, null),
            new ProductVariantInput(null, "M", null, "SKU-M", 11m, "USD", true, null)
        };
        product.SetVariants(options, initial).IsSuccess.Should().BeTrue();

        var sVariantId = product.Variants.Single(v => v.Option1Value == "S").Id;
        var mVariantId = product.Variants.Single(v => v.Option1Value == "M").Id;

        // Second replace: keep S (matched by Id, new price), omit M (must be deactivated, not deleted).
        var second = new[] { new ProductVariantInput(sVariantId, "S", null, "SKU-S", 12m, "USD", true, null) };
        var result = product.SetVariants(options, second);

        result.IsSuccess.Should().BeTrue();
        product.Variants.Should().HaveCount(2);
        var sVariant = product.Variants.Single(v => v.Id == sVariantId);
        sVariant.Price.Amount.Should().Be(12m);
        sVariant.IsActive.Should().BeTrue();

        var mVariant = product.Variants.Single(v => v.Id == mVariantId);
        mVariant.IsActive.Should().BeFalse();

        product.MinPrice.Should().Be(12m);
        product.MaxPrice.Should().Be(12m);
    }

    [Fact]
    public void Publish_Should_Succeed_When_ActiveVariantExists()
    {
        var product = CreateLegacy();

        var result = product.Publish();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void SetVariants_Should_Fail_When_DeactivatingTheOnlyActiveVariant()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var variants = new[] { new ProductVariantInput(null, null, null, "SKU-X", 10m, "USD", true, null) };
        product.SetVariants(Array.Empty<ProductOptionInput>(), variants).IsSuccess.Should().BeTrue();
        var onlyVariantId = product.Variants.Single().Id;

        var result = product.SetVariants(Array.Empty<ProductOptionInput>(), new[]
        {
            new ProductVariantInput(onlyVariantId, null, null, "SKU-X", 10m, "USD", false, null)
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.NoActiveVariant");
        product.Variants.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public void Update_Should_Fail_When_ProductHasOptions()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var options = new[] { new ProductOptionInput("Size", new[] { "S" }) };
        var variants = new[] { new ProductVariantInput(null, "S", null, "SKU-X", 10m, "USD", true, null) };
        product.SetVariants(options, variants);

        var result = product.Update("New Name", null, Money.Create(20m, "USD"), null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.LegacyPriceUpdateNotAllowed");
    }

    [Fact]
    public void Update_Should_UpdateDefaultVariantPrice_When_SingleVariantNoOptions()
    {
        var product = CreateLegacy(10m);

        var result = product.Update("New Name", "desc", Money.Create(25m, "USD"), null, "Brand");

        result.IsSuccess.Should().BeTrue();
        product.Variants.Single().Price.Amount.Should().Be(25m);
        product.MinPrice.Should().Be(25m);
        product.MaxPrice.Should().Be(25m);
    }

    [Fact]
    public void RemoveImage_Should_ClearVariantImageId_When_ImageReferencedByVariant()
    {
        var product = Product.CreateDraft("P", "p", null, null, null);
        var image = product.AddImage("https://img/1.png", null, "pid-1");
        var variants = new[] { new ProductVariantInput(null, null, null, "SKU-X", 10m, "USD", true, image.Id) };
        product.SetVariants(Array.Empty<ProductOptionInput>(), variants);

        var removeResult = product.RemoveImage(image.Id);

        removeResult.IsSuccess.Should().BeTrue();
        product.Variants.Single().ImageId.Should().BeNull();
    }
}

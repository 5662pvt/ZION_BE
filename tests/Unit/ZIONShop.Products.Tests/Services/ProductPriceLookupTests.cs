using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Services;

namespace ZIONShop.Products.Tests.Services;

public class ProductPriceLookupTests
{
    private static ProductsDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ProductsDbContext(options);
    }

    [Fact]
    public async Task GetAsync_Should_ResolveByVariantId_When_IdIsAVariant()
    {
        using var db = NewDb();
        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);
        product.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new ProductVariantInput(null, "S", null, "SKU-S", 50m, "VND", true, null),
                new ProductVariantInput(null, "M", null, "SKU-M", 60m, "VND", true, null)
            });
        product.MarkCreated();
        product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var variantId = product.Variants.Single(v => v.Option1Value == "M").Id;
        var lookup = new ProductPriceLookup(db);

        var info = await lookup.GetAsync(variantId, CancellationToken.None);

        info.Should().NotBeNull();
        info!.VariantId.Should().Be(variantId);
        info.UnitPrice.Should().Be(60m);
        info.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_ResolveByProductId_When_ExactlyOneActiveVariant()
    {
        using var db = NewDb();
        var product = Product.Create("Legacy", "legacy", Sku.Create("SKU-LEGACY"), Money.Create(100m, "USD"), null, null, null);
        product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var lookup = new ProductPriceLookup(db);

        var info = await lookup.GetAsync(product.Id, CancellationToken.None);

        info.Should().NotBeNull();
        info!.VariantId.Should().Be(product.Id);
        info.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_Should_ResolveVariantImageUrl_When_VariantHasOwnImage()
    {
        using var db = NewDb();
        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);
        var redImage = product.AddImage("https://cdn/red.png", null, null);
        product.SetVariants(
            new[] { new ProductOptionInput("Color", new[] { "Red", "Blue" }) },
            new[]
            {
                new ProductVariantInput(null, "Red", null, "SKU-RED", 50m, "VND", true, redImage.Id),
                new ProductVariantInput(null, "Blue", null, "SKU-BLUE", 50m, "VND", true, null)
            });
        product.MarkCreated();
        product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var variantId = product.Variants.Single(v => v.Option1Value == "Red").Id;
        var lookup = new ProductPriceLookup(db);

        var info = await lookup.GetAsync(variantId, CancellationToken.None);

        info.Should().NotBeNull();
        info!.VariantImageUrl.Should().Be("https://cdn/red.png");
    }

    [Fact]
    public async Task GetAsync_Should_HaveNullVariantImageUrl_When_VariantHasNoOwnImage()
    {
        using var db = NewDb();
        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);
        product.AddImage("https://cdn/primary.png", null, null);
        product.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new ProductVariantInput(null, "S", null, "SKU-S3", 50m, "VND", true, null),
                new ProductVariantInput(null, "M", null, "SKU-M3", 60m, "VND", true, null)
            });
        product.MarkCreated();
        product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var variantId = product.Variants.Single(v => v.Option1Value == "M").Id;
        var lookup = new ProductPriceLookup(db);

        var info = await lookup.GetAsync(variantId, CancellationToken.None);

        info.Should().NotBeNull();
        info!.VariantImageUrl.Should().BeNull();
        info.PrimaryImageUrl.Should().Be("https://cdn/primary.png");
    }

    [Fact]
    public async Task GetAsync_Should_BeUnavailable_When_ProductIdHasMultipleActiveVariants()
    {
        using var db = NewDb();
        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);
        product.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new ProductVariantInput(null, "S", null, "SKU-S2", 50m, "VND", true, null),
                new ProductVariantInput(null, "M", null, "SKU-M2", 60m, "VND", true, null)
            });
        product.MarkCreated();
        product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var lookup = new ProductPriceLookup(db);

        var info = await lookup.GetAsync(product.Id, CancellationToken.None);

        info.Should().NotBeNull();
        info!.IsAvailable.Should().BeFalse();
        info.VariantId.Should().BeNull();
    }
}

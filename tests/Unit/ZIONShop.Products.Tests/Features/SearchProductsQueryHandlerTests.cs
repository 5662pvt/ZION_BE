using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.SearchProducts;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;

namespace ZIONShop.Products.Tests.Features;

public class SearchProductsQueryHandlerTests
{
    private static async Task<(ProductsDbContext, ProductRepository, CategoryRepository)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var cat = Category.Create("Books", "books");
        await db.Categories.AddAsync(cat);

        for (int i = 1; i <= 25; i++)
        {
            var p = Product.Create($"Product {i}", $"product-{i}", Sku.Create($"SKU-{i:D3}"), Money.Create(10m + i, "USD"), cat.Id, null, null);
            p.Publish();
            await db.Products.AddAsync(p);
        }
        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), new CategoryRepository(db));
    }

    [Fact]
    public async Task Handle_Should_ReturnPagedPublishedProducts_When_NoFilter()
    {
        var (db, products, categories) = await SeedAsync();
        var handler = new SearchProductsQueryHandler(products, categories);
        var result = await handler.Handle(new SearchProductsQuery(1, 10, null, null, null, null, "name"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(10);
        result.Value.TotalCount.Should().Be(25);
        result.Value.Page.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_FilterByPriceRange_When_MinMaxProvided()
    {
        var (db, products, categories) = await SeedAsync();
        var handler = new SearchProductsQueryHandler(products, categories);
        var result = await handler.Handle(new SearchProductsQuery(1, 100, null, null, 20m, 25m, "price_asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.All(i => i.Price is >= 20m and <= 25m).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_FilterAndSortByMinPrice_When_ProductHasMultipleVariants()
    {
        var (db, products, categories) = await SeedAsync();
        var cat = await db.Categories.FirstAsync();

        var variantProduct = Product.CreateDraft("Variant Shirt", "variant-shirt", cat.Id, null, null);
        variantProduct.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new ProductVariantInput(null, "S", null, "SKU-VS-S", 5m, "USD", true, null),
                new ProductVariantInput(null, "M", null, "SKU-VS-M", 999m, "USD", true, null)
            });
        variantProduct.MarkCreated();
        variantProduct.Publish();
        await db.Products.AddAsync(variantProduct);
        await db.SaveChangesAsync();

        var handler = new SearchProductsQueryHandler(products, categories);
        var result = await handler.Handle(new SearchProductsQuery(1, 100, "Variant Shirt", null, null, null, "price_asc"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.Price.Should().Be(5m);
        item.MinPrice.Should().Be(5m);
        item.MaxPrice.Should().Be(999m);
        item.VariantCount.Should().Be(2);
        item.Sku.Should().Be("SKU-VS-S");
    }

    [Fact]
    public async Task Handle_Should_NotMatchKeyword_When_OnlyAnInactiveVariantSkuContainsIt()
    {
        var (db, products, categories) = await SeedAsync();

        var variantProduct = Product.CreateDraft("Inactive Variant Widget", "inactive-variant-widget", null, null, null);
        variantProduct.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S" }) },
            new[] { new ProductVariantInput(null, "S", null, "SKU-GHOST-001", 5m, "USD", true, null) });
        variantProduct.MarkCreated();
        variantProduct.Publish();
        await db.Products.AddAsync(variantProduct);
        await db.SaveChangesAsync();

        // Deactivate the only variant by omitting it from a new (empty-options) SetVariants call.
        var variantIdsBefore = variantProduct.Variants.Select(v => v.Id).ToHashSet();
        variantProduct.SetVariants(
            Array.Empty<ProductOptionInput>(),
            new[] { new ProductVariantInput(null, null, null, "SKU-ACTIVE-001", 6m, "USD", true, null) });
        foreach (var variant in variantProduct.Variants.Where(v => !variantIdsBefore.Contains(v.Id)))
            db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();

        var handler = new SearchProductsQueryHandler(products, categories);
        var result = await handler.Handle(new SearchProductsQuery(1, 100, "SKU-GHOST-001", null, null, null, "name"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().NotContain(i => i.Id == variantProduct.Id);
    }
}

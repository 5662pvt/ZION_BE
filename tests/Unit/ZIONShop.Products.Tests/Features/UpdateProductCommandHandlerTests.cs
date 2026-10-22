using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Products.Application.Features.UpdateProduct;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class UpdateProductCommandHandlerTests
{
    private static async Task<(ProductsDbContext db, ProductRepository products, CategoryRepository categories, Product product)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-UPD1"), Money.Create(10m, "USD"), null, null, null);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), new CategoryRepository(db), product);
    }

    [Fact]
    public async Task Handle_Should_InvalidateDetailCache_When_UpdateSucceeds()
    {
        var (db, products, categories, product) = await SeedAsync();
        var cache = new FakeDistributedCacheService();
        await cache.SetAsync($"zion:products:v2:detail:id:{product.Id}", new object());
        await cache.SetAsync($"zion:products:v2:detail:slug:{product.Slug}", new object());
        var handler = new UpdateProductCommandHandler(products, categories, db, cache);

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "Updated", "Desc", 15m, "USD", null, "Brand"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cache.Contains($"zion:products:v2:detail:id:{product.Id}").Should().BeFalse();
        cache.Contains($"zion:products:v2:detail:slug:{product.Slug}").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_UpdateNameWithoutTouchingPrice_When_ProductHasOptionsAndPriceOmitted()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var products = new ProductRepository(db);
        var categories = new CategoryRepository(db);

        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);
        product.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S" }) },
            new[] { new ProductVariantInput(null, "S", null, "SKU-UPD2", 10m, "VND", true, null) });
        product.MarkCreated();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var cache = new FakeDistributedCacheService();
        var handler = new UpdateProductCommandHandler(products, categories, db, cache);

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "Renamed Shirt", "new desc", null, null, null, "Brand"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Renamed Shirt");
        result.Value.Price.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_Should_AddExactlyOneProductUpdatedOutboxEvent_When_UpdateSucceeds()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var products = new ProductRepository(db);
        var categories = new CategoryRepository(db);

        var product = Product.CreateDraft("Shirt", "shirt", null, null, null);
        product.SetVariants(
            new[] { new ProductOptionInput("Size", new[] { "S" }) },
            new[] { new ProductVariantInput(null, "S", null, "SKU-UPD3", 10m, "VND", true, null) });
        product.MarkCreated();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var fakeUow = new FakeProductsUnitOfWork();
        var handler = new UpdateProductCommandHandler(products, categories, fakeUow, new FakeDistributedCacheService());

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "Renamed Shirt", "new desc", null, null, null, "Brand"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fakeUow.OutboxEvents.Should().ContainSingle();
        var evt = fakeUow.OutboxEvents.Single().Should().BeOfType<ProductUpdatedIntegrationEvent>().Subject;
        evt.ProductId.Should().Be(product.Id);
        evt.Name.Should().Be("Renamed Shirt");
        evt.Variants.Should().ContainSingle(v => v.Sku == "SKU-UPD3" && v.Label == "S");
    }
}

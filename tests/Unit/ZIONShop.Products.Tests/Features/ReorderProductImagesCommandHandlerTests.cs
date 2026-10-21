using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.ReorderProductImages;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class ReorderProductImagesCommandHandlerTests
{
    private static async Task<(ProductsDbContext db, ProductRepository products, Product product)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-RO1"), Money.Create(10m, "USD"), null, null, null);
        product.AddImage("https://cdn.test/1.png", "One", "pid-1");
        product.AddImage("https://cdn.test/2.png", "Two", "pid-2");
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), product);
    }

    [Fact]
    public async Task Handle_Should_ReorderImages_When_IdsMatchCurrentSet()
    {
        var (db, products, product) = await SeedAsync();
        var ids = product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Id).Reverse().ToList();
        var cache = new FakeDistributedCacheService();
        await cache.SetAsync($"zion:products:v2:detail:id:{product.Id}", new object());
        var handler = new ReorderProductImagesCommandHandler(products, db, cache);

        var result = await handler.Handle(new ReorderProductImagesCommand(product.Id, ids), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await products.GetByIdAsync(product.Id, CancellationToken.None);
        reloaded!.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Id).Should().BeEquivalentTo(ids, o => o.WithStrictOrdering());
        cache.Contains($"zion:products:v2:detail:id:{product.Id}").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_IdsMismatchCurrentSet()
    {
        var (db, products, product) = await SeedAsync();
        var mismatchedIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var handler = new ReorderProductImagesCommandHandler(products, db, new FakeDistributedCacheService());

        var result = await handler.Handle(new ReorderProductImagesCommand(product.Id, mismatchedIds), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.InvalidImageOrder");
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        await cache.SetAsync($"zion:products:detail:id:{product.Id}", new object());
        await cache.SetAsync($"zion:products:detail:slug:{product.Slug}", new object());
        var handler = new UpdateProductCommandHandler(products, categories, db, cache);

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "Updated", "Desc", 15m, "USD", null, "Brand"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cache.Contains($"zion:products:detail:id:{product.Id}").Should().BeFalse();
        cache.Contains($"zion:products:detail:slug:{product.Slug}").Should().BeFalse();
    }
}

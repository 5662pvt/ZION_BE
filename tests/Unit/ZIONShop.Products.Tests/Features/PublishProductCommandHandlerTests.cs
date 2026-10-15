using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.PublishProduct;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class PublishProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_InvalidateDetailCache_When_PublishSucceeds()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-PUB1"), Money.Create(10m, "USD"), null, null, null);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var products = new ProductRepository(db);
        var categories = new CategoryRepository(db);
        var cache = new FakeDistributedCacheService();
        await cache.SetAsync($"zion:products:detail:id:{product.Id}", new object());
        await cache.SetAsync($"zion:products:detail:slug:{product.Slug}", new object());
        var handler = new PublishProductCommandHandler(products, categories, db, cache);

        var result = await handler.Handle(new PublishProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Published");
        cache.Contains($"zion:products:detail:id:{product.Id}").Should().BeFalse();
        cache.Contains($"zion:products:detail:slug:{product.Slug}").Should().BeFalse();
    }
}

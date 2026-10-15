using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.ArchiveProduct;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class ArchiveProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_InvalidateDetailCache_When_ArchiveSucceeds()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-ARC1"), Money.Create(10m, "USD"), null, null, null);
        product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();

        var products = new ProductRepository(db);
        var categories = new CategoryRepository(db);
        var cache = new FakeDistributedCacheService();
        await cache.SetAsync($"zion:products:detail:id:{product.Id}", new object());
        await cache.SetAsync($"zion:products:detail:slug:{product.Slug}", new object());
        var handler = new ArchiveProductCommandHandler(products, categories, db, cache);

        var result = await handler.Handle(new ArchiveProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Archived");
        cache.Contains($"zion:products:detail:id:{product.Id}").Should().BeFalse();
        cache.Contains($"zion:products:detail:slug:{product.Slug}").Should().BeFalse();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.Categories;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class CategoryCommandHandlerTests
{
    private const string TreeKey = "zion:categories:tree";

    private static (ProductsDbContext db, CategoryRepository categories, FakeDistributedCacheService cache) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        return (db, new CategoryRepository(db), new FakeDistributedCacheService());
    }

    [Fact]
    public async Task Handle_Should_InvalidateTreeCache_When_CategoryCreated()
    {
        var (db, categories, cache) = BuildScope();
        await cache.SetAsync(TreeKey, new object());
        var handler = new CreateCategoryCommandHandler(categories, db, cache);

        var result = await handler.Handle(new CreateCategoryCommand("Electronics", "electronics", null, null, 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cache.Contains(TreeKey).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_InvalidateTreeCache_When_CategoryUpdated()
    {
        var (db, categories, cache) = BuildScope();
        var category = Category.Create("Electronics", "electronics");
        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();
        await cache.SetAsync(TreeKey, new object());
        var handler = new UpdateCategoryCommandHandler(categories, db, cache);

        var result = await handler.Handle(new UpdateCategoryCommand(category.Id, "Electronics 2", "Desc", 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cache.Contains(TreeKey).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_InvalidateTreeCache_When_CategoryDeleted()
    {
        var (db, categories, cache) = BuildScope();
        var category = Category.Create("Electronics", "electronics");
        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();
        await cache.SetAsync(TreeKey, new object());
        var handler = new DeleteCategoryCommandHandler(categories, new ProductRepository(db), db, cache);

        var result = await handler.Handle(new DeleteCategoryCommand(category.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cache.Contains(TreeKey).Should().BeFalse();
    }
}

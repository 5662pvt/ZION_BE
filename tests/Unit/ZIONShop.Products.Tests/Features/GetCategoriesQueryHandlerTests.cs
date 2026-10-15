using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.Categories;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class GetCategoriesQueryHandlerTests
{
    private sealed class CountingCategoryRepository : ICategoryRepository
    {
        private readonly CategoryRepository _inner;
        public int QueryCallCount { get; private set; }

        public CountingCategoryRepository(CategoryRepository inner) => _inner = inner;

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _inner.GetByIdAsync(id, cancellationToken);
        public Task<Category?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => _inner.GetBySlugAsync(slug, cancellationToken);
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) => _inner.SlugExistsAsync(slug, cancellationToken);
        public Task AddAsync(Category category, CancellationToken cancellationToken = default) => _inner.AddAsync(category, cancellationToken);
        public void Update(Category category) => _inner.Update(category);
        public void Remove(Category category) => _inner.Remove(category);

        public IQueryable<Category> Query()
        {
            QueryCallCount++;
            return _inner.Query();
        }
    }

    private static async Task<(CountingCategoryRepository categories, CategoryRepository raw)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var category = Category.Create("Electronics", "electronics");
        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();
        var raw = new CategoryRepository(db);
        return (new CountingCategoryRepository(raw), raw);
    }

    [Fact]
    public async Task Handle_Should_HitCache_When_AsTreeCalledTwice()
    {
        var (categories, _) = await SeedAsync();
        var cache = new FakeDistributedCacheService();
        var handler = new GetCategoriesQueryHandler(categories, cache);

        var first = await handler.Handle(new GetCategoriesQuery(AsTree: true), CancellationToken.None);
        var second = await handler.Handle(new GetCategoriesQuery(AsTree: true), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        categories.QueryCallCount.Should().Be(1);
        cache.Contains("zion:categories:tree").Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_BypassCache_When_FlatListRequested()
    {
        var (categories, _) = await SeedAsync();
        var cache = new FakeDistributedCacheService();
        var handler = new GetCategoriesQueryHandler(categories, cache);

        await handler.Handle(new GetCategoriesQuery(AsTree: false), CancellationToken.None);
        await handler.Handle(new GetCategoriesQuery(AsTree: false), CancellationToken.None);

        categories.QueryCallCount.Should().Be(2);
        cache.Contains("zion:categories:tree").Should().BeFalse();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.GetProduct;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class GetProductByIdQueryHandlerTests
{
    private sealed class CountingProductRepository : IProductRepository
    {
        private readonly ProductRepository _inner;
        public int GetByIdCallCount { get; private set; }

        public CountingProductRepository(ProductRepository inner) => _inner = inner;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCallCount++;
            return _inner.GetByIdAsync(id, cancellationToken);
        }

        public Task<Product?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default) => _inner.GetBySlugAsync(slug, cancellationToken);
        public Task<bool> SkuExistsAsync(string sku, Guid? excludeProductId = null, CancellationToken cancellationToken = default) => _inner.SkuExistsAsync(sku, excludeProductId, cancellationToken);
        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default) => _inner.SlugExistsAsync(slug, cancellationToken);
        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => _inner.AddAsync(product, cancellationToken);
        public void Update(Product product) => _inner.Update(product);
        public void AddImage(ProductImage image) => _inner.AddImage(image);
        public void AddVariant(ProductVariant variant) => _inner.AddVariant(variant);
        public IQueryable<Product> Query() => _inner.Query();
    }

    private static async Task<(CountingProductRepository products, CategoryRepository categories, Product product)> SeedAsync(bool published)
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-GET1"), Money.Create(10m, "USD"), null, null, null);
        if (published) product.Publish();
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        return (new CountingProductRepository(new ProductRepository(db)), new CategoryRepository(db), product);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ProductNotPublished()
    {
        var (products, categories, product) = await SeedAsync(published: false);
        var cache = new FakeDistributedCacheService();
        var handler = new GetProductByIdQueryHandler(products, categories, cache);

        var result = await handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        cache.Contains("zion:products:v2:detail:id:" + product.Id).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_HitCache_When_CalledTwiceForPublishedProduct()
    {
        var (products, categories, product) = await SeedAsync(published: true);
        var cache = new FakeDistributedCacheService();
        var handler = new GetProductByIdQueryHandler(products, categories, cache);

        var first = await handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);
        var second = await handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        products.GetByIdCallCount.Should().Be(1);
    }
}

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
}

using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.SuggestProducts;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;

namespace ZIONShop.Products.Tests.Features;

public class SuggestProductsQueryHandlerTests
{
    private static async Task<(ProductsDbContext db, ProductRepository products)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);

        var matching = Product.Create("Blue Widget", "blue-widget", Sku.Create("WDG-001"), Money.Create(10m, "USD"), null, null, "Acme");
        matching.Publish();
        matching.AddImage("https://img/blue-1.png", null, 1);
        matching.AddImage("https://img/blue-0.png", null, 0);
        await db.Products.AddAsync(matching);

        var startsWithQuery = Product.Create("Widget Pro", "widget-pro", Sku.Create("WDG-002"), Money.Create(12m, "USD"), null, null, "Acme");
        startsWithQuery.Publish();
        await db.Products.AddAsync(startsWithQuery);

        var draft = Product.Create("Widget Draft", "widget-draft", Sku.Create("WDG-003"), Money.Create(14m, "USD"), null, null, "Acme");
        await db.Products.AddAsync(draft);

        await db.SaveChangesAsync();
        return (db, new ProductRepository(db));
    }

    [Fact]
    public async Task Handle_Should_ReturnMatches_With_Thumbnail_When_QueryIsTwoChars()
    {
        var (_, products) = await SeedAsync();
        var handler = new SuggestProductsQueryHandler(products);

        var result = await handler.Handle(new SuggestProductsQuery("wi", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(i => i.Name == "Blue Widget" && i.ThumbnailUrl == "https://img/blue-0.png");
    }

    [Fact]
    public async Task Handle_Should_ExcludeNonPublished_Products()
    {
        var (_, products) = await SeedAsync();
        var handler = new SuggestProductsQueryHandler(products);

        var result = await handler.Handle(new SuggestProductsQuery("wi", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain(i => i.Name == "Widget Draft");
    }

    [Fact]
    public async Task Handle_Should_OrderStartsWithMatchesFirst()
    {
        var (_, products) = await SeedAsync();
        var handler = new SuggestProductsQueryHandler(products);

        var result = await handler.Handle(new SuggestProductsQuery("widget", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.First().Name.Should().Be("Widget Pro");
    }

    [Fact]
    public async Task Handle_Should_ReturnBadRequest_When_QueryIsOneChar()
    {
        var (_, products) = await SeedAsync();
        var handler = new SuggestProductsQueryHandler(products);

        var result = await handler.Handle(new SuggestProductsQuery("w", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.InvalidSuggestQuery");
    }

    [Fact]
    public async Task Handle_Should_RespectLimit()
    {
        var (_, products) = await SeedAsync();
        var handler = new SuggestProductsQueryHandler(products);

        var result = await handler.Handle(new SuggestProductsQuery("wi", 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }
}

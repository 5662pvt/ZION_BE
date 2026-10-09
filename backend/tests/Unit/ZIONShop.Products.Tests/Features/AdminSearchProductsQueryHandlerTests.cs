using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.AdminSearchProducts;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;

namespace ZIONShop.Products.Tests.Features;

public class AdminSearchProductsQueryHandlerTests
{
    private static async Task<(ProductsDbContext db, ProductRepository products, CategoryRepository categories)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);

        var published = Product.Create("Published Item", "published-item", Sku.Create("SKU-P01"), Money.Create(10m, "USD"), null, null, null);
        published.Publish();
        await db.Products.AddAsync(published);

        var draft = Product.Create("Draft Item", "draft-item", Sku.Create("SKU-D01"), Money.Create(20m, "USD"), null, null, null);
        await db.Products.AddAsync(draft);

        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), new CategoryRepository(db));
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyDraftProducts_When_StatusFilterIsDraft()
    {
        var (_, products, categories) = await SeedAsync();
        var handler = new AdminSearchProductsQueryHandler(products, categories);

        var result = await handler.Handle(new AdminSearchProductsQuery(1, 20, null, null, ProductStatus.Draft), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle();
        result.Value.Items.Single().Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_Should_ReturnAllStatuses_When_NoStatusFilterProvided()
    {
        var (_, products, categories) = await SeedAsync();
        var handler = new AdminSearchProductsQueryHandler(products, categories);

        var result = await handler.Handle(new AdminSearchProductsQuery(1, 20, null, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_Should_FindProduct_When_KeywordCaseDiffersFromStoredSku()
    {
        var (_, products, categories) = await SeedAsync();
        var handler = new AdminSearchProductsQueryHandler(products, categories);

        var result = await handler.Handle(new AdminSearchProductsQuery(1, 20, "sku-p01", null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(p => p.Sku == "SKU-P01");
    }
}

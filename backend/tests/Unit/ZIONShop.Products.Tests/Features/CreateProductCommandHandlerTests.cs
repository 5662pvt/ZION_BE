using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.CreateProduct;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.Repositories;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;

namespace ZIONShop.Products.Tests.Features;

public class CreateProductCommandHandlerTests
{
    private static (ProductsDbContext db, ProductRepository products, CategoryRepository categories) BuildScope()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        return (db, new ProductRepository(db), new CategoryRepository(db));
    }

    [Fact]
    public async Task Handle_Should_CreatePublishedProduct_When_PayloadValid()
    {
        var (db, products, categories) = BuildScope();
        var category = Category.Create("Electronics", "electronics");
        await db.Categories.AddAsync(category);
        await db.SaveChangesAsync();

        var handler = new CreateProductCommandHandler(products, categories, db);
        var cmd = new CreateProductCommand("Test Product", "test-product", "TST-001", 19.99m, "USD", category.Id, "Desc", "TestBrand", null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Sku.Should().Be("TST-001");
        result.Value.Status.Should().Be("Published");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_SkuAlreadyExists()
    {
        var (db, products, categories) = BuildScope();
        var existing = Product.Create("Existing", "existing", Sku.Create("DUP-001"), Money.Create(10m, "USD"), null, null, null);
        await db.Products.AddAsync(existing);
        await db.SaveChangesAsync();

        var handler = new CreateProductCommandHandler(products, categories, db);
        var cmd = new CreateProductCommand("New", "new-slug", "DUP-001", 12m, "USD", null, null, null, null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.SkuAlreadyExists");
    }
}

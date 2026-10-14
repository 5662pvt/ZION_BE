using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.AdminGetProduct;
using ZIONShop.Products.Application.Features.GetProduct;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;

namespace ZIONShop.Products.Tests.Features;

public class AdminGetProductByIdQueryHandlerTests
{
    private static async Task<(Product draft, ProductRepository products, CategoryRepository categories)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);

        var draft = Product.Create("Draft Item", "draft-item", Sku.Create("SKU-D01"), Money.Create(20m, "USD"), null, null, null);
        await db.Products.AddAsync(draft);
        await db.SaveChangesAsync();

        return (draft, new ProductRepository(db), new CategoryRepository(db));
    }

    [Fact]
    public async Task Handle_Should_ReturnProduct_When_StatusIsDraft()
    {
        var (draft, products, categories) = await SeedAsync();
        var handler = new AdminGetProductByIdQueryHandler(products, categories);

        var result = await handler.Handle(new AdminGetProductByIdQuery(draft.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ProductDoesNotExist()
    {
        var (_, products, categories) = await SeedAsync();
        var handler = new AdminGetProductByIdQueryHandler(products, categories);

        var result = await handler.Handle(new AdminGetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_PublicQueryGivenDraftProduct()
    {
        var (draft, products, categories) = await SeedAsync();
        var handler = new GetProductByIdQueryHandler(products, categories);

        var result = await handler.Handle(new GetProductByIdQuery(draft.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Application.Features.AddProductImage;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Tests.Features;

public class AddProductImageCommandHandlerTests
{
    private class FakeImageStorage : IImageStorage
    {
        public Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct)
            => Task.FromResult(Result.Success(new StoredImage("https://cdn.test/image.png", "zionshop/products/image")));

        public Task DeleteAsync(string publicId, CancellationToken ct) => Task.CompletedTask;
    }

    private static async Task<(ProductsDbContext db, ProductRepository products, Product product)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-IMG1"), Money.Create(10m, "USD"), null, null, null);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), product);
    }

    [Fact]
    public async Task Handle_Should_AddImage_When_UploadSucceeds()
    {
        var (db, products, product) = await SeedAsync();
        var cache = new FakeDistributedCacheService();
        await cache.SetAsync($"zion:products:detail:id:{product.Id}", new object());
        var handler = new AddProductImageCommandHandler(products, new FakeImageStorage(), db, cache);

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var cmd = new AddProductImageCommand(product.Id, content, "photo.png", "image/png", content.Length, "Alt text");

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Url.Should().Be("https://cdn.test/image.png");

        var reloaded = await products.GetByIdAsync(product.Id, CancellationToken.None);
        reloaded!.Images.Should().ContainSingle();
        cache.Contains($"zion:products:detail:id:{product.Id}").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ProductMissing()
    {
        var (db, products, _) = await SeedAsync();
        var handler = new AddProductImageCommandHandler(products, new FakeImageStorage(), db, new FakeDistributedCacheService());

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var cmd = new AddProductImageCommand(Guid.NewGuid(), content, "photo.png", "image/png", content.Length, null);

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.ProductNotFound");
    }

    [Fact]
    public void Validator_Should_Fail_When_ContentTypeNotAllowed()
    {
        var validator = new AddProductImageCommandValidator();
        using var content = new MemoryStream(new byte[] { 1 });
        var cmd = new AddProductImageCommand(Guid.NewGuid(), content, "file.gif", "image/gif", 1, null);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
    }
}

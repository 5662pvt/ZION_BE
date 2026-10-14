using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Products.Application.Features.RemoveProductImage;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Tests.Features;

public class RemoveProductImageCommandHandlerTests
{
    private class FakeImageStorage : IImageStorage
    {
        public int DeleteCallCount { get; private set; }

        public Task<Result<StoredImage>> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct)
            => Task.FromResult(Result.Success(new StoredImage("https://cdn.test/image.png", "public-id-1")));

        public Task DeleteAsync(string publicId, CancellationToken ct)
        {
            DeleteCallCount++;
            return Task.CompletedTask;
        }
    }

    private static async Task<(ProductsDbContext db, ProductRepository products, Product product)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Test Product", "test-product", Sku.Create("SKU-RM1"), Money.Create(10m, "USD"), null, null, null);
        product.AddImage("https://cdn.test/image.png", "Alt", "public-id-1");
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), product);
    }

    [Fact]
    public async Task Handle_Should_RemoveImageAndDeleteFromStorage_When_ImageExists()
    {
        var (db, products, product) = await SeedAsync();
        var imageId = product.Images.Single().Id;
        var storage = new FakeImageStorage();
        var handler = new RemoveProductImageCommandHandler(products, storage, db, NullLogger<RemoveProductImageCommandHandler>.Instance);

        var result = await handler.Handle(new RemoveProductImageCommand(product.Id, imageId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        storage.DeleteCallCount.Should().Be(1);

        var reloaded = await products.GetByIdAsync(product.Id, CancellationToken.None);
        reloaded!.Images.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ImageMissing()
    {
        var (db, products, product) = await SeedAsync();
        var storage = new FakeImageStorage();
        var handler = new RemoveProductImageCommandHandler(products, storage, db, NullLogger<RemoveProductImageCommandHandler>.Instance);

        var result = await handler.Handle(new RemoveProductImageCommand(product.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.ImageNotFound");
    }
}

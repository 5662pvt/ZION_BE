using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Products.Application.Features.SetVariants;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.Products.Infrastructure.Persistence;
using ZIONShop.Products.Infrastructure.Repositories;
using ZIONShop.Products.Tests.TestSupport;

namespace ZIONShop.Products.Tests.Features;

public class SetVariantsCommandHandlerTests
{
    private static async Task<(ProductsDbContext db, ProductRepository products, CategoryRepository categories, Product product)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<ProductsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ProductsDbContext(options);
        var product = Product.Create("Shirt", "shirt", Sku.Create("SKU-SV1"), Money.Create(100m, "VND"), null, null, null);
        await db.Products.AddAsync(product);
        await db.SaveChangesAsync();
        return (db, new ProductRepository(db), new CategoryRepository(db), product);
    }

    [Fact]
    public async Task Handle_Should_ReplaceVariants_And_InvalidateCache_When_PayloadValid()
    {
        var (db, products, categories, product) = await SeedAsync();
        var cache = new FakeDistributedCacheService();
        await cache.SetAsync($"zion:products:v2:detail:id:{product.Id}", new object());
        var handler = new SetVariantsCommandHandler(products, categories, db, cache);

        var cmd = new SetVariantsCommand(
            product.Id,
            new[] { new SetVariantsOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new SetVariantsVariantInput(null, "S", null, "SKU-S", 50m, "VND", true, null),
                new SetVariantsVariantInput(null, "M", null, "SKU-M", 60m, "VND", true, null)
            });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Options.Should().ContainSingle();
        // The original default variant (SKU-SV1) is deactivated, never deleted, so it still shows up
        // alongside the 2 new active ones in the admin (includeInactiveVariants: true) view.
        result.Value.Variants.Should().HaveCount(3);
        result.Value.Variants.Should().ContainSingle(v => v.Sku == "SKU-SV1" && !v.IsActive);
        result.Value.Variants.Where(v => v.IsActive).Should().HaveCount(2);
        result.Value.MinPrice.Should().Be(50m);
        result.Value.MaxPrice.Should().Be(60m);
        cache.Contains($"zion:products:v2:detail:id:{product.Id}").Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_ProductMissing()
    {
        var (_, products, categories, _) = await SeedAsync();
        var handler = new SetVariantsCommandHandler(products, categories, new ProductsDbContext(
            new DbContextOptionsBuilder<ProductsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options), new FakeDistributedCacheService());

        var cmd = new SetVariantsCommand(Guid.NewGuid(), Array.Empty<SetVariantsOptionInput>(), new[]
        {
            new SetVariantsVariantInput(null, null, null, "SKU-X", 10m, "VND", true, null)
        });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.ProductNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnConflict_When_VariantSkuUsedByAnotherProduct()
    {
        var (db, products, categories, product) = await SeedAsync();
        var other = Product.Create("Other", "other", Sku.Create("SKU-OTHER"), Money.Create(20m, "USD"), null, null, null);
        await db.Products.AddAsync(other);
        await db.SaveChangesAsync();

        var handler = new SetVariantsCommandHandler(products, categories, db, new FakeDistributedCacheService());
        var cmd = new SetVariantsCommand(product.Id, Array.Empty<SetVariantsOptionInput>(), new[]
        {
            new SetVariantsVariantInput(null, null, null, "SKU-OTHER", 10m, "VND", true, null)
        });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.SkuAlreadyExists");
    }

    [Fact]
    public async Task Handle_Should_ReuseOmittedVariantsSku_When_RegeneratingMatrixWithANewRowUsingIt()
    {
        var (db, products, categories, product) = await SeedAsync();
        var handler = new SetVariantsCommandHandler(products, categories, db, new FakeDistributedCacheService());

        var firstCmd = new SetVariantsCommand(
            product.Id,
            new[] { new SetVariantsOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new SetVariantsVariantInput(null, "S", null, "SKU-REGEN-S", 50m, "VND", true, null),
                new SetVariantsVariantInput(null, "M", null, "SKU-REGEN-M", 60m, "VND", true, null)
            });
        (await handler.Handle(firstCmd, CancellationToken.None)).IsSuccess.Should().BeTrue();

        // Regenerate the matrix: omit "S" and introduce a new "L" row that reuses SKU-REGEN-S. Must return
        // 200 (reusing the omitted row), not fail with a duplicate-key error from the DB.
        var secondCmd = new SetVariantsCommand(
            product.Id,
            new[] { new SetVariantsOptionInput("Size", new[] { "M", "L" }) },
            new[]
            {
                new SetVariantsVariantInput(null, "L", null, "SKU-REGEN-S", 70m, "VND", true, null),
                new SetVariantsVariantInput(null, "M", null, "SKU-REGEN-M", 60m, "VND", true, null)
            });
        var result = await handler.Handle(secondCmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Original default variant (SKU-SV1, deactivated by the first call) + the 2 reused/updated ones.
        result.Value.Variants.Should().HaveCount(3);
        result.Value.Variants.Where(v => v.IsActive).Should().HaveCount(2);
        result.Value.Variants.Should().ContainSingle(v => v.Sku == "SKU-REGEN-S" && v.Label == "L");
    }

    [Fact]
    public async Task Handle_Should_AllowReusingProductsOwnExistingSku_When_UpdatingSameVariantInPlace()
    {
        var (db, products, categories, product) = await SeedAsync();
        var handler = new SetVariantsCommandHandler(products, categories, db, new FakeDistributedCacheService());
        var defaultVariantId = product.Variants.Single().Id;

        // SKU-SV1 belongs to this product's own default variant; resubmitting it by Id (in-place update)
        // must not conflict with itself via the global-uniqueness SkuExistsAsync(excludeProductId) check.
        var cmd = new SetVariantsCommand(product.Id, Array.Empty<SetVariantsOptionInput>(), new[]
        {
            new SetVariantsVariantInput(defaultVariantId, null, null, "SKU-SV1", 10m, "VND", true, null)
        });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_AddExactlyOneProductUpdatedOutboxEvent_When_VariantsChange()
    {
        var (db, products, categories, product) = await SeedAsync();
        var fakeUow = new FakeProductsUnitOfWork();
        var handler = new SetVariantsCommandHandler(products, categories, fakeUow, new FakeDistributedCacheService());

        var cmd = new SetVariantsCommand(
            product.Id,
            new[] { new SetVariantsOptionInput("Size", new[] { "S", "M" }) },
            new[]
            {
                new SetVariantsVariantInput(null, "S", null, "SKU-OUTBOX-S", 50m, "VND", true, null),
                new SetVariantsVariantInput(null, "M", null, "SKU-OUTBOX-M", 60m, "VND", true, null)
            });

        var result = await handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fakeUow.OutboxEvents.Should().ContainSingle();
        var evt = fakeUow.OutboxEvents.Single().Should().BeOfType<ProductUpdatedIntegrationEvent>().Subject;
        evt.ProductId.Should().Be(product.Id);
        evt.Name.Should().Be(product.Name);
        evt.Variants.Should().ContainSingle(v => v.Sku == "SKU-OUTBOX-S" && v.Label == "S");
        evt.Variants.Should().ContainSingle(v => v.Sku == "SKU-OUTBOX-M" && v.Label == "M");
    }
}

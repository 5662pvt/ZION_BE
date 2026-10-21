using Moq;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Application.Services;
using ZIONShop.Inventory.Domain.Exceptions;

namespace ZIONShop.Inventory.Tests.Services;

public class ProductOrVariantIdResolverTests
{
    [Fact]
    public async Task ResolveAsync_Should_ReturnVariantId_When_DefaultVariantIsActive()
    {
        var productId = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, true, "Published"));

        var result = await ProductOrVariantIdResolver.ResolveAsync(catalog.Object, productId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(productId);
    }

    [Fact]
    public async Task ResolveAsync_Should_FallThroughToProduct_When_DefaultVariantIsDeactivated()
    {
        // Product converted to options: the legacy default variant (Id == ProductId) was deactivated,
        // leaving exactly one NEW active variant. Must resolve to the active variant, not the dead default.
        var productId = Guid.NewGuid();
        var activeVariantId = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published"));
        catalog.Setup(c => c.ListVariantsByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published"),
                new ProductVariantCatalogItem(activeVariantId, productId, "SKU-B", "Product A", "One Size", true, "Published")
            });

        var result = await ProductOrVariantIdResolver.ResolveAsync(catalog.Object, productId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(activeVariantId);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnVariantRequired_When_DefaultVariantDeactivated_And_NoActiveVariantsRemain()
    {
        var productId = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published"));
        catalog.Setup(c => c.ListVariantsByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published")
            });

        var result = await ProductOrVariantIdResolver.ResolveAsync(catalog.Object, productId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.VariantRequired);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnVariantRequired_When_DefaultVariantDeactivated_And_SeveralActiveVariantsRemain()
    {
        var productId = Guid.NewGuid();
        var variantA = Guid.NewGuid();
        var variantB = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        catalog.Setup(c => c.GetVariantAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published"));
        catalog.Setup(c => c.ListVariantsByProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ProductVariantCatalogItem(productId, productId, "SKU-A", "Product A", string.Empty, false, "Published"),
                new ProductVariantCatalogItem(variantA, productId, "SKU-B", "Product A", "S", true, "Published"),
                new ProductVariantCatalogItem(variantB, productId, "SKU-C", "Product A", "M", true, "Published")
            });

        var result = await ProductOrVariantIdResolver.ResolveAsync(catalog.Object, productId, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.VariantRequired);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnProductNotFound_When_IdMatchesNeitherVariantNorProduct()
    {
        var id = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        catalog.Setup(c => c.GetVariantAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((ProductVariantCatalogItem?)null);
        catalog.Setup(c => c.ListVariantsByProductAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ProductVariantCatalogItem>());

        var result = await ProductOrVariantIdResolver.ResolveAsync(catalog.Object, id, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.ProductNotFound);
    }

    [Fact]
    public async Task ResolveAsync_Should_ReturnVariantId_When_GenuineNonDefaultVariantIsInactive()
    {
        // VariantId != ProductId here: this is not the "dead default variant" ambiguity the fix targets,
        // so the caller's explicit (if inactive) variant id is still taken directly.
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var catalog = new Mock<IProductCatalogReader>(MockBehavior.Strict);
        catalog.Setup(c => c.GetVariantAsync(variantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductVariantCatalogItem(variantId, productId, "SKU-B", "Product A", "S", false, "Published"));

        var result = await ProductOrVariantIdResolver.ResolveAsync(catalog.Object, variantId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(variantId);
    }
}

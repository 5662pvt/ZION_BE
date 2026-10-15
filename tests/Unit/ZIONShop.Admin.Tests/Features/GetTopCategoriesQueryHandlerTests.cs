using Moq;
using ZIONShop.Admin.Application.Common;
using ZIONShop.Admin.Application.Features.GetTopCategories;
using ZIONShop.Admin.Tests.TestSupport;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;

namespace ZIONShop.Admin.Tests.Features;

public class GetTopCategoriesQueryHandlerTests
{
    private static readonly FakeTimeProvider Now = new(new DateTimeOffset(2026, 10, 13, 4, 0, 0, TimeSpan.Zero));

    private static CompletedOrderRow OrderWith(params ReportOrderLine[] lines) => new(
        Guid.NewGuid(), new DateTime(2026, 10, 3, 10, 0, 0, DateTimeKind.Utc),
        0m, 0m, 0m, "COD", null, null, lines);

    [Fact]
    public async Task Handle_Should_MapProductToItsCategory_When_CategoryExists()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var order = OrderWith(new ReportOrderLine(productId, "Product A", 2, 100m, false, null));

        var orders = new Mock<IOrderReportReader>();
        orders.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { order });

        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCatalogItem(productId, "SKU-A", "Product A", "Published", categoryId) });

        var categories = new Mock<ICategoryLookup>();
        categories.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CategoryRef(categoryId, "Electronics") });

        var handler = new GetTopCategoriesQueryHandler(orders.Object, catalog.Object, categories.Object, Now, new FakeDistributedCacheService());
        var result = await handler.Handle(new GetTopCategoriesQuery("2026-10-01", "2026-10-07", "revenue"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var category = result.Value.Should().ContainSingle().Subject;
        category.CategoryId.Should().Be(categoryId);
        category.CategoryName.Should().Be("Electronics");
        category.Revenue.Should().Be(200m);
    }

    [Fact]
    public async Task Handle_Should_UseUncategorizedLabel_When_ProductHasNoCategory()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var productId = Guid.NewGuid();

        var order = OrderWith(new ReportOrderLine(productId, "Orphan Product", 1, 50m, false, null));

        var orders = new Mock<IOrderReportReader>();
        orders.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { order });

        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProductCatalogItem(productId, "SKU-B", "Orphan Product", "Published", null) });

        var categories = new Mock<ICategoryLookup>();
        categories.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CategoryRef>());

        var handler = new GetTopCategoriesQueryHandler(orders.Object, catalog.Object, categories.Object, Now, new FakeDistributedCacheService());
        var result = await handler.Handle(new GetTopCategoriesQuery("2026-10-01", "2026-10-07", "revenue"), CancellationToken.None);

        var category = result.Value.Should().ContainSingle().Subject;
        category.CategoryId.Should().BeNull();
        category.CategoryName.Should().Be("Chưa phân loại");
    }

    [Fact]
    public async Task Handle_Should_HitCache_When_SameParametersRequestedTwice()
    {
        var range = ReportRange.Create("2026-10-01", "2026-10-07", Now).Value;
        var order = OrderWith(new ReportOrderLine(Guid.NewGuid(), "Product A", 1, 10m, false, null));

        var orders = new Mock<IOrderReportReader>();
        orders.Setup(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { order });

        var catalog = new Mock<IProductCatalogReader>();
        catalog.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ProductCatalogItem>());

        var categories = new Mock<ICategoryLookup>();
        categories.Setup(x => x.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<CategoryRef>());

        var cache = new FakeDistributedCacheService();
        var handler = new GetTopCategoriesQueryHandler(orders.Object, catalog.Object, categories.Object, Now, cache);

        await handler.Handle(new GetTopCategoriesQuery("2026-10-01", "2026-10-07", "revenue"), CancellationToken.None);
        await handler.Handle(new GetTopCategoriesQuery("2026-10-01", "2026-10-07", "revenue"), CancellationToken.None);

        orders.Verify(x => x.GetCompletedOrdersAsync(range.FromUtc, range.ToUtc, It.IsAny<CancellationToken>()), Times.Once);
    }
}

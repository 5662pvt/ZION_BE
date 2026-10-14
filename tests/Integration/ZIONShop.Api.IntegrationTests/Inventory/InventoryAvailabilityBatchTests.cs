using System.Net;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Inventory;

[Collection(ApiTestCollection.Name)]
public class InventoryAvailabilityBatchTests
{
    private readonly ApiFactory _factory;

    public InventoryAvailabilityBatchTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task GetAvailabilityBatch_Should_ReturnAvailability_And_FalseInStockForUnknownProduct()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 7);
        var unknownProductId = Guid.NewGuid();

        var results = await GetAvailabilityBatchAsync(admin, new[] { productId, unknownProductId });

        results.Should().HaveCount(2);
        var seeded = results.Single(r => r.ProductId == productId);
        seeded.Available.Should().Be(7);
        seeded.InStock.Should().BeTrue();

        var unknown = results.Single(r => r.ProductId == unknownProductId);
        unknown.Available.Should().Be(0);
        unknown.InStock.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailabilityBatch_Should_Return400_When_ProductIdIsNotAGuid()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/inventory/availability?productIds=not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

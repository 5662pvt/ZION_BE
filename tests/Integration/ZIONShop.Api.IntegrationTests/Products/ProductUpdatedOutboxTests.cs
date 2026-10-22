using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Products;

/// <summary>
/// Products now publishes <c>ProductUpdatedIntegrationEvent</c> through its own transactional outbox
/// (same mechanism as Reviews/Orders/Inventory) when a product is renamed. Inventory consumes it to
/// refresh the denormalized ProductName snapshot it carries on every row for that product. Like
/// <see cref="CheckoutTestHelpers.WaitForOrderStatusAsync"/>, the update settles asynchronously after
/// OutboxPublisherService's own polling interval, so this test polls rather than asserting inline.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ProductUpdatedOutboxTests
{
    private readonly ApiFactory _factory;

    public ProductUpdatedOutboxTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task RenameProduct_Should_UpdateInventoryProductName_AfterOutboxDispatches()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var newName = $"Renamed Product {Guid.NewGuid():N}";
        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}", new
        {
            name = newName,
            description = (string?)null,
            price = (decimal?)null,
            currency = (string?)null,
            categoryId = (Guid?)null,
            brand = (string?)null
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var inventoryItem = await WaitForInventoryAsync(admin, sku, i => i.ProductName == newName);

        inventoryItem.ProductName.Should().Be(newName);
        inventoryItem.ProductId.Should().Be(productId);
    }
}

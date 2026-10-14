using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Inventory;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Inventory;

[Collection(ApiTestCollection.Name)]
public class StockAvailabilityReaderTests
{
    private readonly ApiFactory _factory;

    public StockAvailabilityReaderTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetAvailableAsync_Should_ReturnAvailable_ForSeededProduct_And_Zero_ForUnknownProduct()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 9);
        var unknownProductId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var reader = scope.ServiceProvider.GetRequiredService<IStockAvailabilityReader>();

        var result = await reader.GetAvailableAsync(new[] { productId, unknownProductId });

        result.Should().HaveCount(2);
        result[productId].Should().Be(9);
        result[unknownProductId].Should().Be(0);
    }
}

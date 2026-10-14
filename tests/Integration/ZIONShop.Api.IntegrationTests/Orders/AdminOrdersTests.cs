using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Orders;

[Collection(ApiTestCollection.Name)]
public class AdminOrdersTests
{
    private readonly ApiFactory _factory;

    public AdminOrdersTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task AdminSearch_Should_FindOrder_When_KeywordMatchesPhoneNumber()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        var results = await AdminSearchOrdersAsync(admin, keyword: "0901234567");

        results.Should().Contain(o => o.Id == placed.Id);
    }

    [Fact]
    public async Task AdminSearch_Should_FindOrder_When_KeywordMatchesRecipientNameCaseInsensitive()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        var results = await AdminSearchOrdersAsync(admin, keyword: "nguyen van test");

        var found = results.Should().ContainSingle(o => o.Id == placed.Id).Subject;
        found.RecipientName.Should().Be("Nguyen Van Test");
        found.PhoneNumber.Should().Be("0901234567");
    }

    [Fact]
    public async Task GetOrder_Should_ReturnHistoryOrderedByAt_When_CodOrderIsConfirmed()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        var confirmed = await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        confirmed.History.Select(h => h.Status).Should().ContainInOrder("PendingStock", "Confirmed");
        confirmed.History.Should().BeInAscendingOrder(h => h.At);
    }
}

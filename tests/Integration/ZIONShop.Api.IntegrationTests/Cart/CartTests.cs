using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Cart;

[Collection(ApiTestCollection.Name)]
public class CartTests
{
    private const string CartTokenHeader = "X-Cart-Token";

    private readonly ApiFactory _factory;

    public CartTests(ApiFactory factory) => _factory = factory;

    private record ProductFull(Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status, Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images);
    private record CartItem(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal, string Currency);
    private record CartData(Guid Id, Guid? UserId, string? AnonymousId, decimal Subtotal, string Currency, int TotalQuantity, List<CartItem> Items);

    private async Task<Guid> CreatePublishedProductAsync(HttpClient admin)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZTest Cart Product {token}",
            slug = $"ztest-cart-product-{token}",
            sku = $"ZT-CART-{token}",
            price = 15.00m,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for cart",
            brand = "ZTestBrand",
            images = Array.Empty<object>()
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        return productId;
    }

    [Fact]
    public async Task GuestCart_Should_HoldItem_Then_MergeIntoUserCart_OnLogin()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        var productId = await CreatePublishedProductAsync(admin);

        var guest = _factory.CreateClient();
        var addResponse = await guest.PostAsJsonAsync("/api/v1/cart/items", new { productId, quantity = 2 });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        addResponse.Headers.TryGetValues(CartTokenHeader, out var tokenValues).Should().BeTrue();
        var cartToken = tokenValues!.First();

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cart");
        getRequest.Headers.Add(CartTokenHeader, cartToken);
        var getResponse = await guest.SendAsync(getRequest);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var guestCart = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<CartData>>(ApiEnvelopeJson.Options);
        guestCart!.Data!.Items.Should().Contain(i => i.ProductId == productId && i.Quantity == 2);

        var mergeRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/cart/merge");
        mergeRequest.Headers.Add(CartTokenHeader, cartToken);
        var mergeResponse = await admin.SendAsync(mergeRequest);
        mergeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userCartResponse = await admin.GetAsync("/api/v1/cart");
        var userCart = await userCartResponse.Content.ReadFromJsonAsync<ApiEnvelope<CartData>>(ApiEnvelopeJson.Options);
        userCart!.Data!.Items.Should().Contain(i => i.ProductId == productId && i.Quantity == 2);
    }
}

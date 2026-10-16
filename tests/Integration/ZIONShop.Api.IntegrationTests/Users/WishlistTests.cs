using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Users;

public record WishlistItemData(Guid ProductId, string Name, string Slug, decimal Price, string Currency, string? ThumbnailUrl, bool InStock, DateTime AddedAt);
public record WishlistToggleData(Guid ProductId, bool InWishlist);

[Collection(ApiTestCollection.Name)]
public class WishlistTests
{
    private readonly ApiFactory _factory;

    public WishlistTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<List<WishlistItemData>> GetWishlistAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/users/me/wishlist");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<WishlistItemData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<List<Guid>> GetWishlistIdsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/users/me/wishlist/ids");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<Guid>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    [Fact]
    public async Task AddToWishlist_Should_AppearInListAndIds_When_ProductIsPublished()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var addResponse = await customer.PutAsync($"/api/v1/users/me/wishlist/{productId}", null);
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var added = await addResponse.Content.ReadFromJsonAsync<ApiEnvelope<WishlistToggleData>>(ApiEnvelopeJson.Options);
        added!.Data!.InWishlist.Should().BeTrue();

        var list = await GetWishlistAsync(customer);
        list.Should().ContainSingle(i => i.ProductId == productId);

        var ids = await GetWishlistIdsAsync(customer);
        ids.Should().ContainSingle(id => id == productId);
    }

    [Fact]
    public async Task AddToWishlist_Should_BeIdempotent_When_CalledTwice()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        (await customer.PutAsync($"/api/v1/users/me/wishlist/{productId}", null)).EnsureSuccessStatusCode();
        (await customer.PutAsync($"/api/v1/users/me/wishlist/{productId}", null)).EnsureSuccessStatusCode();

        var list = await GetWishlistAsync(customer);
        list.Should().ContainSingle(i => i.ProductId == productId);
    }

    [Fact]
    public async Task AddToWishlist_Should_ReturnNotFound_When_ProductDoesNotExist()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await customer.PutAsync($"/api/v1/users/me/wishlist/{Guid.NewGuid()}", null);

        ((int)response.StatusCode).Should().Be(404);
    }

    [Fact]
    public async Task RemoveFromWishlist_Should_RemoveItem_And_BeIdempotent()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        (await customer.PutAsync($"/api/v1/users/me/wishlist/{productId}", null)).EnsureSuccessStatusCode();

        var removeResponse = await customer.DeleteAsync($"/api/v1/users/me/wishlist/{productId}");
        removeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var removed = await removeResponse.Content.ReadFromJsonAsync<ApiEnvelope<WishlistToggleData>>(ApiEnvelopeJson.Options);
        removed!.Data!.InWishlist.Should().BeFalse();

        (await GetWishlistAsync(customer)).Should().BeEmpty();

        var secondRemove = await customer.DeleteAsync($"/api/v1/users/me/wishlist/{productId}");
        secondRemove.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wishlist_Should_BeIsolatedPerCustomer()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);

        var (customerA, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);

        (await customerA.PutAsync($"/api/v1/users/me/wishlist/{productId}", null)).EnsureSuccessStatusCode();

        (await GetWishlistAsync(customerA)).Should().ContainSingle(i => i.ProductId == productId);
        (await GetWishlistAsync(customerB)).Should().BeEmpty();
    }

    [Fact]
    public async Task Wishlist_Endpoints_Should_ReturnUnauthorized_When_Anonymous()
    {
        var anonymous = _factory.CreateClient();
        var productId = Guid.NewGuid();

        ((int)(await anonymous.GetAsync("/api/v1/users/me/wishlist")).StatusCode).Should().Be(401);
        ((int)(await anonymous.GetAsync("/api/v1/users/me/wishlist/ids")).StatusCode).Should().Be(401);
        ((int)(await anonymous.PutAsync($"/api/v1/users/me/wishlist/{productId}", null)).StatusCode).Should().Be(401);
        ((int)(await anonymous.DeleteAsync($"/api/v1/users/me/wishlist/{productId}")).StatusCode).Should().Be(401);
    }
}

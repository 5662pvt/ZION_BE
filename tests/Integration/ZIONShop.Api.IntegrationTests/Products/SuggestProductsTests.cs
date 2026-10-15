using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Products;

[Collection(ApiTestCollection.Name)]
public class SuggestProductsTests
{
    private readonly ApiFactory _factory;

    public SuggestProductsTests(ApiFactory factory) => _factory = factory;

    private record ProductSuggestion(Guid Id, string Name, string Slug, string Sku, decimal Price, string Currency, string? ThumbnailUrl);
    private record ProductFull(Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status, Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images);

    private static object BuildCreateBody(string token) => new
    {
        name = $"ZSuggest Widget {token}",
        slug = $"zsuggest-widget-{token}",
        sku = $"ZS-{token}",
        price = 19.99m,
        currency = "VND",
        categoryId = (Guid?)null,
        description = "integration test product",
        brand = "ZSuggestBrand",
        images = Array.Empty<object>()
    };

    [Fact]
    public async Task Suggest_Should_ReturnMatches_With_Thumbnail_When_QueryIsTwoChars()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = _factory.CreateClient();
        var suggestResponse = await anonymous.GetAsync($"/api/v1/products/suggest?q={token}");
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await suggestResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSuggestion>>>(ApiEnvelopeJson.Options);

        envelope!.Data.Should().ContainSingle(p => p.Id == productId);
    }

    [Fact]
    public async Task Suggest_Should_ExcludeNonPublishedProducts()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = _factory.CreateClient();
        var suggestResponse = await anonymous.GetAsync($"/api/v1/products/suggest?q={token}");
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await suggestResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSuggestion>>>(ApiEnvelopeJson.Options);

        envelope!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Suggest_Should_Return400_When_QueryIsOneChar()
    {
        var anonymous = _factory.CreateClient();
        var suggestResponse = await anonymous.GetAsync("/api/v1/products/suggest?q=a");
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await suggestResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSuggestion>>>(ApiEnvelopeJson.Options);

        envelope!.Errors.Should().ContainSingle(e => e.Code == "Products.InvalidSuggestQuery");
    }

    [Fact]
    public async Task Suggest_Should_RespectLimit()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 3; i++)
        {
            var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody($"{token}-{i}"));
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
            var publishResponse = await admin.PostAsync($"/api/v1/products/{created!.Data!.Id}/publish", null);
            publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var anonymous = _factory.CreateClient();
        var suggestResponse = await anonymous.GetAsync($"/api/v1/products/suggest?q={token}&limit=2");
        suggestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await suggestResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSuggestion>>>(ApiEnvelopeJson.Options);

        envelope!.Data.Should().HaveCount(2);
    }
}

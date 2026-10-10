using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Cart;

[Collection(ApiTestCollection.Name)]
public class CartEnrichmentTests
{
    private readonly ApiFactory _factory;

    public CartEnrichmentTests(ApiFactory factory) => _factory = factory;

    private record ProductFull(Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status, Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images);
    private record ProductImage(Guid Id, string Url, string? Alt, int DisplayOrder);
    private record CartItem(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal, string Currency, string? ProductSlug, string? ImageUrl);
    private record CartData(Guid Id, Guid? UserId, string? AnonymousId, decimal Subtotal, string Currency, int TotalQuantity, List<CartItem> Items);

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private async Task<(Guid ProductId, string Slug, string ImageUrl)> CreatePublishedProductWithImageAsync(HttpClient admin)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var slug = $"ztest-cart-enrich-{token}";
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZTest Cart Enrich Product {token}",
            slug,
            sku = $"ZT-ENR-{token}",
            price = 15.00m,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for cart enrichment",
            brand = "ZTestBrand",
            images = Array.Empty<object>()
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "tiny.png");
        var uploadResponse = await admin.PostAsync($"/api/v1/products/{productId}/images", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductImage>>(ApiEnvelopeJson.Options);

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return (productId, slug, uploaded!.Data!.Url);
    }

    [Fact]
    public async Task GetCart_Should_IncludeProductSlugAndImageUrl_When_ProductHasImage()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        var (productId, slug, imageUrl) = await CreatePublishedProductWithImageAsync(admin);

        var addResponse = await admin.PostAsJsonAsync("/api/v1/cart/items", new { productId, quantity = 1 });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await admin.GetAsync("/api/v1/cart");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cart = await getResponse.Content.ReadFromJsonAsync<ApiEnvelope<CartData>>(ApiEnvelopeJson.Options);

        var item = cart!.Data!.Items.Should().ContainSingle(i => i.ProductId == productId).Subject;
        item.ProductSlug.Should().Be(slug);
        item.ImageUrl.Should().Be(imageUrl);
    }
}

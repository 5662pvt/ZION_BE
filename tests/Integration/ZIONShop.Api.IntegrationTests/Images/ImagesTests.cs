using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Images;

[Collection(ApiTestCollection.Name)]
public class ImagesTests
{
    // 1x1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly ApiFactory _factory;

    public ImagesTests(ApiFactory factory) => _factory = factory;

    private record ProductImage(Guid Id, string Url, string? Alt, int DisplayOrder);
    private record ProductFull(Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status, Guid? CategoryId, string? CategoryName, string? Brand, List<ProductImage> Images);

    private static MultipartFormDataContent BuildUploadContent(string? alt = null)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "tiny.png");
        if (alt is not null)
            content.Add(new StringContent(alt), "alt");
        return content;
    }

    private async Task<Guid> CreateDraftProductAsync(HttpClient admin)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZTest Image Product {token}",
            slug = $"ztest-image-product-{token}",
            sku = $"ZT-IMG-{token}",
            price = 9.99m,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for images",
            brand = "ZTestBrand",
            images = Array.Empty<object>()
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        return created!.Data!.Id;
    }

    [Fact]
    public async Task UploadImage_Should_Return200_And_BeServedAsStaticFile()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        var productId = await CreateDraftProductAsync(admin);

        var uploadResponse = await admin.PostAsync($"/api/v1/products/{productId}/images", BuildUploadContent("alt text"));
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductImage>>(ApiEnvelopeJson.Options);
        var url = uploaded!.Data!.Url;
        var path = Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri) ? absoluteUri.AbsolutePath : url;
        path.Should().StartWith("/uploads/products/");

        var imageResponse = await admin.GetAsync(path);
        imageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        imageResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task ReorderImages_Should_UpdateDisplayOrder()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        var productId = await CreateDraftProductAsync(admin);

        var first = await (await admin.PostAsync($"/api/v1/products/{productId}/images", BuildUploadContent()))
            .Content.ReadFromJsonAsync<ApiEnvelope<ProductImage>>(ApiEnvelopeJson.Options);
        var second = await (await admin.PostAsync($"/api/v1/products/{productId}/images", BuildUploadContent()))
            .Content.ReadFromJsonAsync<ApiEnvelope<ProductImage>>(ApiEnvelopeJson.Options);

        var reorderResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}/images/order", new
        {
            imageIds = new[] { second!.Data!.Id, first!.Data!.Id }
        });
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var productResponse = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        var product = await productResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var ordered = product!.Data!.Images.OrderBy(i => i.DisplayOrder).ToList();
        ordered[0].Id.Should().Be(second.Data.Id);
        ordered[1].Id.Should().Be(first.Data.Id);
    }

    [Fact]
    public async Task DeleteImage_Should_RemoveItFromProduct()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        var productId = await CreateDraftProductAsync(admin);

        var uploaded = await (await admin.PostAsync($"/api/v1/products/{productId}/images", BuildUploadContent()))
            .Content.ReadFromJsonAsync<ApiEnvelope<ProductImage>>(ApiEnvelopeJson.Options);

        var deleteResponse = await admin.DeleteAsync($"/api/v1/products/{productId}/images/{uploaded!.Data!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var productResponse = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        var product = await productResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        product!.Data!.Images.Should().NotContain(i => i.Id == uploaded.Data.Id);
    }
}

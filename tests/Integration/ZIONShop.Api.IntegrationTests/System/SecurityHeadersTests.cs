using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

[Collection(ApiTestCollection.Name)]
public class SecurityHeadersTests
{
    // 1x1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly ApiFactory _factory;

    public SecurityHeadersTests(ApiFactory factory) => _factory = factory;

    private record ProductImage(Guid Id, string Url, string? Alt, int DisplayOrder);
    private record ProductFull(Guid Id, string Name, string Slug, List<ProductImage> Images);

    [Fact]
    public async Task ApiResponse_Should_IncludeSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products?page=1&pageSize=1");

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.Should().ContainKey("Permissions-Policy");
        response.Headers.GetValues("Content-Security-Policy").Should().Contain(v => v.Contains("default-src 'none'"));
    }

    [Fact]
    public async Task Swagger_Should_NotIncludeContentSecurityPolicy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        response.Headers.Should().NotContainKey("Content-Security-Policy");
    }

    [Fact]
    public async Task UploadedImage_Should_StillBeServed_DespiteSecurityHeaders()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZTest SecHeaders Product {token}",
            slug = $"ztest-secheaders-product-{token}",
            sku = $"ZT-SEC-{token}",
            price = 9.99m,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for security headers",
            brand = "ZTestBrand",
            images = Array.Empty<object>()
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "tiny.png");

        var uploadResponse = await admin.PostAsync($"/api/v1/products/{productId}/images", content);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductImage>>(ApiEnvelopeJson.Options);
        var url = uploaded!.Data!.Url;
        var path = Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri) ? absoluteUri.AbsolutePath : url;

        var imageResponse = await admin.GetAsync(path);

        imageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        imageResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }
}

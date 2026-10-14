using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Products;

[Collection(ApiTestCollection.Name)]
public class ProductsTests
{
    private readonly ApiFactory _factory;

    public ProductsTests(ApiFactory factory) => _factory = factory;

    private record ProductSummary(Guid Id, string Name, string Slug, string Sku, decimal Price, string Currency, string Status, string? CategoryName, string? PrimaryImageUrl);
    private record ProductFull(Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status, Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images);

    private static object BuildCreateBody(string token, bool draft = true) => new
    {
        name = $"ZTest Widget {token}",
        slug = $"ztest-widget-{token}",
        sku = $"ZT-{token}",
        price = 19.99m,
        currency = "VND",
        categoryId = (Guid?)null,
        description = "integration test product",
        brand = "ZTestBrand",
        images = Array.Empty<object>()
    };

    [Fact]
    public async Task Search_Should_ReturnOnlyPublished_And_IncludePagination()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];

        var draftResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody($"{token}-draft"));
        draftResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishedCreateResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody($"{token}-pub"));
        publishedCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publishedCreated = await publishedCreateResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var publishResponse = await admin.PostAsync($"/api/v1/products/{publishedCreated!.Data!.Id}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = _factory.CreateClient();
        var searchResponse = await anonymous.GetAsync($"/api/v1/products?keyword={token}&page=1&pageSize=20");
        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var searchEnvelope = await searchResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSummary>>>(ApiEnvelopeJson.Options);

        searchEnvelope!.Pagination.Should().NotBeNull();
        searchEnvelope.Pagination!.PageSize.Should().Be(20);
        searchEnvelope.Data.Should().ContainSingle();
        searchEnvelope.Data![0].Status.Should().Be("Published");
        searchEnvelope.Data[0].Name.Should().Contain("pub");
    }

    [Fact]
    public async Task AdminSearch_Should_RequireAuthentication_And_Role()
    {
        var anonymous = _factory.CreateClient();
        var unauthenticatedResponse = await anonymous.GetAsync("/api/v1/products/admin");
        unauthenticatedResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var nonAdminEmail = $"non-admin-{Guid.NewGuid():N}@zionshop.local";
        var nonAdminClient = _factory.CreateClient();
        await nonAdminClient.RegisterAndConfirmAsync(_factory, nonAdminEmail, "Passw0rd!123", "Non Admin");
        var nonAdminLoginResponse = await nonAdminClient.LoginAsync(nonAdminEmail, "Passw0rd!123");
        var nonAdminLogin = await nonAdminLoginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        nonAdminClient.WithBearerToken(nonAdminLogin!.Data!.AccessToken);

        var forbiddenResponse = await nonAdminClient.GetAsync("/api/v1/products/admin");
        forbiddenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminSearch_Should_IncludeDraftProducts_When_CalledByAdmin()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminSearchResponse = await admin.GetAsync($"/api/v1/products/admin?keyword={token}");
        adminSearchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminSearch = await adminSearchResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSummary>>>(ApiEnvelopeJson.Options);
        adminSearch!.Data.Should().ContainSingle(p => p.Status == "Draft");
    }

    [Fact]
    public async Task Create_Should_BeDraftAndHidden_Then_VisibleAfterPublish()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;
        created.Data.Status.Should().Be("Draft");

        var anonymous = _factory.CreateClient();
        var publicGetBeforePublish = await anonymous.GetAsync($"/api/v1/products/{productId}");
        publicGetBeforePublish.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var adminGetDraft = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        adminGetDraft.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publicGetAfterPublish = await anonymous.GetAsync($"/api/v1/products/{productId}");
        publicGetAfterPublish.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProduct = await publicGetAfterPublish.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        publicProduct!.Data!.Status.Should().Be("Published");
    }
}

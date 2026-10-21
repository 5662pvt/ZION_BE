using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Products;

[Collection(ApiTestCollection.Name)]
public class VariantsTests
{
    private readonly ApiFactory _factory;

    public VariantsTests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);
    private record ProductSummary(Guid Id, string Name, string Slug, string Sku, decimal Price, string Currency, string Status, string? CategoryName, string? PrimaryImageUrl, decimal MinPrice, decimal MaxPrice, int VariantCount);

    private static object BuildCreateBody(string token) => new
    {
        name = $"ZVariant Widget {token}",
        slug = $"zvariant-widget-{token}",
        sku = $"ZV-{token}",
        price = 19.99m,
        currency = "VND",
        categoryId = (Guid?)null,
        description = "integration test product",
        brand = "ZVariantBrand",
        images = Array.Empty<object>()
    };

    [Fact]
    public async Task LegacyCreate_Should_HaveDefaultVariant_WithIdEqualToProductId()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);

        created!.Data!.Variants.Should().ContainSingle();
        created.Data.Variants.Single().Id.Should().Be(created.Data.Id);
        created.Data.Variants.Single().IsActive.Should().BeTrue();
        created.Data.MinPrice.Should().Be(19.99m);
        created.Data.MaxPrice.Should().Be(19.99m);
    }

    [Fact]
    public async Task SetVariants_Should_UpdateMatrix_And_BeVisibleViaSlug_WithInactiveVariantHiddenPublicly()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var variantsBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "S", "M" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "S", option2Value = (string?)null, sku = $"ZV-{token}-S", price = 100_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZV-{token}-M", price = 150_000m, currency = "VND", isActive = false, imageId = (Guid?)null }
            }
        };
        var setResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}/variants", variantsBody);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminGet = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        var adminProduct = await adminGet.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        // Admin view shows ALL variants: the two new ones plus the original default variant, now deactivated.
        adminProduct!.Data!.Variants.Should().HaveCount(3);
        adminProduct.Data.Options.Should().ContainSingle(o => o.Name == "Size");
        adminProduct.Data.MinPrice.Should().Be(100_000m);
        adminProduct.Data.MaxPrice.Should().Be(100_000m);

        var slug = created.Data.Slug;
        var anonymous = _factory.CreateClient();
        var publicResponse = await anonymous.GetAsync($"/api/v1/products/by-slug/{slug}");
        publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var publicProduct = await publicResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);

        // Public view only exposes ACTIVE variants: the "S" variant (active) and NOT "M" (isActive:false) or the deactivated default.
        publicProduct!.Data!.Variants.Should().ContainSingle();
        publicProduct.Data.Variants.Single().Sku.Should().Be($"ZV-{token}-S".ToUpperInvariant());
        publicProduct.Data.Options.Should().ContainSingle(o => o.Name == "Size");
        publicProduct.Data.MinPrice.Should().Be(100_000m);
        publicProduct.Data.MaxPrice.Should().Be(100_000m);
    }

    [Fact]
    public async Task Search_Should_FilterAndSortByMinPrice_And_MatchKeywordAgainstVariantSku()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildCreateBody(token));
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var variantSku = $"ZV-{token}-CHEAP";
        var variantsBody = new
        {
            options = Array.Empty<object>(),
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = (string?)null, option2Value = (string?)null, sku = variantSku, price = 1_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        var setResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}/variants", variantsBody);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var anonymous = _factory.CreateClient();

        var priceFilterResponse = await anonymous.GetAsync($"/api/v1/products?minPrice=500&maxPrice=5000&sort=price_asc");
        priceFilterResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var priceFilterEnvelope = await priceFilterResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSummary>>>(ApiEnvelopeJson.Options);
        priceFilterEnvelope!.Data.Should().Contain(p => p.Id == productId && p.MinPrice == 1_000m);

        // Sku.Create normalizes to upper-invariant and the public search keyword match is case-sensitive
        // (same as before variants existed), so search with the SKU as actually stored.
        var keywordResponse = await anonymous.GetAsync($"/api/v1/products?keyword={variantSku.ToUpperInvariant()}");
        keywordResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var keywordEnvelope = await keywordResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductSummary>>>(ApiEnvelopeJson.Options);
        keywordEnvelope!.Data.Should().ContainSingle(p => p.Id == productId);
    }
}

using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Variants;

/// <summary>
/// Covers the W21 backend review fixes for product variants: wishlist pricing for multi-variant products
/// (Users module), SKU reuse when regenerating the variants matrix, cart add-by-productId after a legacy
/// default variant is deactivated, name-only product updates once a product has options, and re-submitting
/// the same options/variants payload twice.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class VariantsReviewFixesTests
{
    private readonly ApiFactory _factory;

    public VariantsReviewFixesTests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);

    private record WishlistItemData(Guid ProductId, string Name, string Slug, decimal Price, string Currency, string? ThumbnailUrl, bool InStock, DateTime AddedAt, decimal MinPrice, decimal MaxPrice, int VariantCount);
    private record WishlistToggleData(Guid ProductId, bool InWishlist);

    private record CartItemData(Guid Id, Guid ProductId, Guid VariantId, string ProductName, string? VariantLabel, string? Sku, int Quantity, decimal UnitPrice, decimal Subtotal, string Currency, string? ProductSlug, string? ImageUrl);
    private record CartData(Guid Id, Guid? UserId, string? AnonymousId, decimal Subtotal, string Currency, int TotalQuantity, List<CartItemData> Items);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<HttpClient> CustomerClientAsync()
    {
        var client = _factory.CreateClient();
        var email = $"variants-review-{Guid.NewGuid():N}@zionshop.local";
        const string password = "Customer@123";
        await client.RegisterAndConfirmAsync(_factory, email, password, "Variants Review Tester");

        var loginResponse = await client.LoginAsync(email, password);
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        client.WithBearerToken(loginEnvelope!.Data!.AccessToken);
        return client;
    }

    private static object BuildLegacyCreateBody(string token) => new
    {
        name = $"ZReview Widget {token}",
        slug = $"zreview-widget-{token}",
        sku = $"ZRV-{token}",
        price = 19.99m,
        currency = "VND",
        categoryId = (Guid?)null,
        description = "integration test product for review fixes",
        brand = "ZReviewBrand",
        images = Array.Empty<object>()
    };

    private async Task<ProductFull> CreateLegacyProductAsync(HttpClient admin, string token)
    {
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", BuildLegacyCreateBody(token));
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        return created!.Data!;
    }

    private static async Task<HttpResponseMessage> PutVariantsRawAsync(HttpClient admin, Guid productId, object body)
        => await admin.PutAsJsonAsync($"/api/v1/products/{productId}/variants", body);

    private async Task<ProductFull> GetAdminProductAsync(HttpClient admin, Guid productId)
    {
        var response = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    [Fact]
    public async Task Wishlist_Should_ShowMultiVariantProduct_With_MinPriceMaxPriceAndVariantCount()
    {
        var admin = await AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateLegacyProductAsync(admin, token);

        var variantsBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "S", "M" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "S", option2Value = (string?)null, sku = $"ZRV-{token}-S", price = 50_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZRV-{token}-M", price = 70_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        (await PutVariantsRawAsync(admin, product.Id, variantsBody)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/v1/products/{product.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var customer = await CustomerClientAsync();

        var addResponse = await customer.PutAsync($"/api/v1/users/me/wishlist/{product.Id}", null);
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var added = await addResponse.Content.ReadFromJsonAsync<ApiEnvelope<WishlistToggleData>>(ApiEnvelopeJson.Options);
        added!.Data!.InWishlist.Should().BeTrue();

        var listResponse = await customer.GetAsync("/api/v1/users/me/wishlist");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<WishlistItemData>>>(ApiEnvelopeJson.Options);

        var item = list!.Data!.Should().ContainSingle(i => i.ProductId == product.Id).Subject;
        item.MinPrice.Should().Be(50_000m);
        item.MaxPrice.Should().Be(70_000m);
        item.VariantCount.Should().Be(2);
        item.Price.Should().Be(50_000m);
    }

    [Fact]
    public async Task SetVariants_Should_ReuseOmittedVariantsSku_When_RegeneratingMatrix_Should_Return200NotServerError()
    {
        var admin = await AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateLegacyProductAsync(admin, token);

        var firstBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "S", "M" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "S", option2Value = (string?)null, sku = $"ZRV-{token}-S", price = 100_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZRV-{token}-M", price = 150_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        (await PutVariantsRawAsync(admin, product.Id, firstBody)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Regenerate: omit "S" and introduce a new "L" row that reuses SKU ZRV-{token}-S. Before the fix this
        // left a duplicate-key insert (new "L" row + the now-deactivated-but-not-renamed "S" row) that bubbled
        // up as an unhandled DbUpdateException (500) once the migration's unique index rejected it.
        var secondBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "M", "L" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "L", option2Value = (string?)null, sku = $"ZRV-{token}-S", price = 120_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZRV-{token}-M", price = 150_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        var secondResponse = await PutVariantsRawAsync(admin, product.Id, secondBody);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await secondResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        updated!.Data!.Variants.Where(v => v.IsActive).Should().HaveCount(2);
        updated.Data.Variants.Should().ContainSingle(v => v.Sku == $"ZRV-{token}-S".ToUpperInvariant() && v.Option1Value == "L" && v.IsActive);
    }

    [Fact]
    public async Task AddToCartByProductId_Should_Work_When_LegacyProductConvertedToOptionsWithExactlyOneActiveVariant()
    {
        var admin = await AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateLegacyProductAsync(admin, token);

        // Convert to options but omit the legacy default variant (Id == ProductId) entirely, leaving exactly
        // one NEW active variant. Before the fix, ProductPriceLookup matched the deactivated default variant
        // by Id == ProductId first and reported it unavailable, instead of falling through to resolve the
        // single remaining active variant -- so adding to cart by product id failed with Cart.ProductUnavailable.
        var variantsBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "One Size" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "One Size", option2Value = (string?)null, sku = $"ZRV-{token}-OS", price = 80_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        (await PutVariantsRawAsync(admin, product.Id, variantsBody)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/v1/products/{product.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var customer = await CustomerClientAsync();
        var addResponse = await customer.PostAsJsonAsync("/api/v1/cart/items", new { productId = product.Id, variantId = (Guid?)null, quantity = 1 });

        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cartResponse = await customer.GetAsync("/api/v1/cart");
        cartResponse.EnsureSuccessStatusCode();
        var cart = await cartResponse.Content.ReadFromJsonAsync<ApiEnvelope<CartData>>(ApiEnvelopeJson.Options);
        cart!.Data!.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 1);
    }

    [Fact]
    public async Task UpdateProduct_Should_UpdateNameWithoutTouchingPrice_When_ProductHasOptions()
    {
        var admin = await AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateLegacyProductAsync(admin, token);

        var variantsBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "S", "M" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "S", option2Value = (string?)null, sku = $"ZRV-{token}-S", price = 90_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZRV-{token}-M", price = 95_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        (await PutVariantsRawAsync(admin, product.Id, variantsBody)).StatusCode.Should().Be(HttpStatusCode.OK);

        // No price/currency in the body at all -- before the fix, UpdateProductBody.Price was a non-nullable
        // decimal, so the request always carried price=0 and Product.Update always rejected it with
        // LegacyPriceUpdateNotAllowed for any product with options, blocking a plain rename.
        var updateBody = new { name = "Renamed Review Widget", description = "updated description", categoryId = (Guid?)null, brand = "ZReviewBrand" };
        var updateResponse = await admin.PutAsJsonAsync($"/api/v1/products/{product.Id}", updateBody);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        updated!.Data!.Name.Should().Be("Renamed Review Widget");
        updated.Data.MinPrice.Should().Be(90_000m);
        updated.Data.MaxPrice.Should().Be(95_000m);
    }

    [Fact]
    public async Task SetVariants_Should_Succeed_When_PutTwiceWithTheSameOptionsAndVariants()
    {
        var admin = await AdminClientAsync();
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateLegacyProductAsync(admin, token);

        object BuildBody() => new
        {
            options = new[] { new { name = "Size", values = new[] { "S", "M" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "S", option2Value = (string?)null, sku = $"ZRV-{token}-S", price = 100_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZRV-{token}-M", price = 150_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };

        var firstResponse = await PutVariantsRawAsync(admin, product.Id, BuildBody());
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Re-submitting the identical payload re-creates the owned ProductOption rows with the same
        // (ProductId, Position) key every time (Product.SetVariants does _options.Clear() then re-adds).
        var secondResponse = await PutVariantsRawAsync(admin, product.Id, BuildBody());
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminProduct = await GetAdminProductAsync(admin, product.Id);
        adminProduct.Options.Should().ContainSingle(o => o.Name == "Size" && o.Values.SequenceEqual(new[] { "S", "M" }));
        // The second PUT's "S"/"M" rows (no explicit Id) carry the same SKUs as the first PUT's, so they are
        // resolved back onto those same variant rows (reused in place) rather than deactivated-and-recreated
        // -- only the original legacy default variant (from product creation) stays deactivated.
        adminProduct.Variants.Should().HaveCount(3);
        adminProduct.Variants.Where(v => v.IsActive).Should().HaveCount(2);
    }
}

using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Variants;

[Collection(ApiTestCollection.Name)]
public class CartVariantsTests
{
    private readonly ApiFactory _factory;

    public CartVariantsTests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);

    private record CartItemData(Guid Id, Guid ProductId, Guid VariantId, string ProductName, string? VariantLabel, string? Sku, int Quantity, decimal UnitPrice, decimal Subtotal, string Currency, string? ProductSlug, string? ImageUrl);
    private record CartData(Guid Id, Guid? UserId, string? AnonymousId, decimal Subtotal, string Currency, int TotalQuantity, List<CartItemData> Items);

    private static object BuildCreateBody(string token) => new
    {
        name = $"ZCart Variant Widget {token}",
        slug = $"zcart-variant-widget-{token}",
        sku = $"ZCV-{token}",
        price = 19.99m,
        currency = "VND",
        categoryId = (Guid?)null,
        description = "integration test product for cart variants",
        brand = "ZCartVariantBrand",
        images = Array.Empty<object>()
    };

    private async Task<(Guid ProductId, Guid VariantS, Guid VariantM)> CreateTwoVariantProductAsync(HttpClient admin, decimal priceS = 100_000m, decimal priceM = 150_000m)
    {
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
                new { id = (Guid?)null, option1Value = "S", option2Value = (string?)null, sku = $"ZCV-{token}-S", price = priceS, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "M", option2Value = (string?)null, sku = $"ZCV-{token}-M", price = priceM, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        var setResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}/variants", variantsBody);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminGet = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        var adminProduct = await adminGet.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var variantS = adminProduct!.Data!.Variants.Single(v => v.Option1Value == "S" && v.IsActive).Id;
        var variantM = adminProduct.Data.Variants.Single(v => v.Option1Value == "M" && v.IsActive).Id;

        await AdjustStockAsync(admin, variantS, 50);
        await AdjustStockAsync(admin, variantM, 50);

        return (productId, variantS, variantM);
    }

    private static async Task AdjustStockAsync(HttpClient admin, Guid variantId, int delta)
    {
        // This route's {productId} slot accepts EITHER a product id or a variant id (see W21 common
        // context); for a multi-variant product the caller must pass the concrete variant id.
        var response = await admin.PostAsJsonAsync($"/api/v1/inventory/products/{variantId}/adjust", new { delta, reason = "Cart variant integration test seed" });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpClient> CreateLoggedInCustomerAsync(ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"cart-variant-{Guid.NewGuid():N}@zionshop.local";
        const string password = "Customer@123";
        await client.RegisterAndConfirmAsync(factory, email, password, "Cart Variant Tester");

        var loginResponse = await client.LoginAsync(email, password);
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        client.WithBearerToken(loginEnvelope!.Data!.AccessToken);
        return client;
    }

    private static async Task<HttpResponseMessage> AddToCartRawAsync(HttpClient client, Guid? productId, Guid? variantId, int quantity)
        => await client.PostAsJsonAsync("/api/v1/cart/items", new { productId, variantId, quantity });

    private static async Task<CartData> GetCartAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/cart");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CartData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    [Fact]
    public async Task AddItem_Should_CreateTwoLines_When_TwoDifferentVariantsAdded()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var (_, variantS, variantM) = await CreateTwoVariantProductAsync(admin);

        var customer = await CreateLoggedInCustomerAsync(_factory);

        (await AddToCartRawAsync(customer, null, variantS, 1)).EnsureSuccessStatusCode();
        (await AddToCartRawAsync(customer, null, variantM, 2)).EnsureSuccessStatusCode();

        var cart = await GetCartAsync(customer);

        cart.Items.Should().HaveCount(2);
        cart.Items.Should().ContainSingle(i => i.VariantId == variantS && i.Quantity == 1 && i.VariantLabel == "S" && i.Sku!.EndsWith("-S"));
        cart.Items.Should().ContainSingle(i => i.VariantId == variantM && i.Quantity == 2 && i.VariantLabel == "M" && i.Sku!.EndsWith("-M"));
    }

    [Fact]
    public async Task AddItem_Should_MergeQuantity_When_SameVariantAddedTwice()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var (_, variantS, _) = await CreateTwoVariantProductAsync(admin);

        var customer = await CreateLoggedInCustomerAsync(_factory);

        (await AddToCartRawAsync(customer, null, variantS, 1)).EnsureSuccessStatusCode();
        (await AddToCartRawAsync(customer, null, variantS, 2)).EnsureSuccessStatusCode();

        var cart = await GetCartAsync(customer);

        cart.Items.Should().ContainSingle();
        cart.Items[0].VariantId.Should().Be(variantS);
        cart.Items[0].Quantity.Should().Be(3);
    }

    [Fact]
    public async Task AddItem_Should_Fail_When_MultiVariantProductIdUsedWithoutChoosingVariant()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var (productId, _, _) = await CreateTwoVariantProductAsync(admin);

        var customer = await CreateLoggedInCustomerAsync(_factory);

        var response = await AddToCartRawAsync(customer, productId, null, 1);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Cart.ProductUnavailable");
    }
}

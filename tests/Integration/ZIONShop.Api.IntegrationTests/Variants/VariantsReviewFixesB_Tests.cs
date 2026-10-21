using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.FlashSales.FlashSaleTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Variants;

/// <summary>
/// Covers the W21-FIX-BE-B review fixes for product variants: the legacy product-id inventory route must
/// resolve to the single ACTIVE variant (never a deactivated default variant left behind by SetVariants),
/// product-level availability must only count ACTIVE variants, flash-sale/gift setup must reject an
/// inactive variant, and a flash-priced order line must be priced at the campaign ACTUALLY reserved rather
/// than a stale cheaper preview.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class VariantsReviewFixesB_Tests
{
    private readonly ApiFactory _factory;

    public VariantsReviewFixesB_Tests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);

    private record AvailabilityData(Guid ProductId, Guid VariantId, string? VariantLabel, int Available, bool InStock);
    private record ProductAvailabilityData(Guid ProductId, int Available, bool InStock);
    private record InventoryItemData(Guid ProductId, Guid VariantId, string? VariantLabel, string Sku, string ProductName, int OnHand, int Reserved, int Available, int LowStockThreshold, bool IsLowStock);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static object BuildLegacyCreateBody(string token) => new
    {
        name = $"ZReviewB Widget {token}",
        slug = $"zreviewb-widget-{token}",
        sku = $"ZRB-{token}",
        price = 19.99m,
        currency = "VND",
        categoryId = (Guid?)null,
        description = "integration test product for review fixes B",
        brand = "ZReviewBBrand",
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

    /// <summary>Converts a legacy (single default-variant) product to options with exactly ONE new active
    /// variant, leaving the original default variant (Id == ProductId) deactivated forever.</summary>
    private async Task<(ProductFull Product, Guid ActiveVariantId)> CreateProductConvertedToOneActiveVariantAsync(HttpClient admin, decimal price = 80_000m)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var product = await CreateLegacyProductAsync(admin, token);

        var variantsBody = new
        {
            options = new[] { new { name = "Size", values = new[] { "One Size" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "One Size", option2Value = (string?)null, sku = $"ZRB-{token}-OS", price, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        (await PutVariantsRawAsync(admin, product.Id, variantsBody)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/v1/products/{product.Id}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var adminGet = await admin.GetAsync($"/api/v1/products/admin/{product.Id}");
        var adminProduct = await adminGet.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var activeVariantId = adminProduct!.Data!.Variants.Single(v => v.IsActive).Id;

        return (product, activeVariantId);
    }

    private static async Task<InventoryItemData> AdjustVariantStockAsync(HttpClient admin, Guid variantId, int delta)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/inventory/variants/{variantId}/adjust", new { delta, reason = "VariantsReviewFixesB seed" });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<InventoryItemData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    [Fact]
    public async Task LegacyProductIdRoute_Should_ReturnTheActiveVariant_When_ProductConvertedToOptionsWithOneActiveVariant()
    {
        var admin = await AdminClientAsync();
        var (product, activeVariantId) = await CreateProductConvertedToOneActiveVariantAsync(admin);

        var response = await admin.GetAsync($"/api/v1/inventory/products/{product.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AvailabilityData>>(ApiEnvelopeJson.Options);
        envelope!.Data!.VariantId.Should().Be(activeVariantId, "the legacy route must resolve to the active variant, not the deactivated default");
        envelope.Data.ProductId.Should().Be(product.Id);
    }

    [Fact]
    public async Task ProductAvailability_Should_ShowOutOfStock_When_StockOnlyOnDeactivatedDefaultVariant()
    {
        var admin = await AdminClientAsync();
        var (product, _) = await CreateProductConvertedToOneActiveVariantAsync(admin);

        // Put stock only on the dead default variant (Id == ProductId), reachable only via the explicit
        // variant route now that the legacy route no longer resolves ambiguous ids to it.
        await AdjustVariantStockAsync(admin, product.Id, 10);

        var response = await admin.GetAsync($"/api/v1/inventory/availability/products?productIds={product.Id}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductAvailabilityData>>>(ApiEnvelopeJson.Options);
        var availability = envelope!.Data!.Single(p => p.ProductId == product.Id);

        availability.Available.Should().Be(0, "stock on an inactive variant must not count toward product availability");
        availability.InStock.Should().BeFalse();
    }

    [Fact]
    public async Task AddFlashSaleItem_Should_BeRejected_When_VariantIsInactive()
    {
        var admin = await AdminClientAsync();
        var (product, _) = await CreateProductConvertedToOneActiveVariantAsync(admin);
        var flashSale = await CreateFlashSaleAsync(admin, $"ZReviewB Flash {Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));

        // product.Id is also the id of the now-deactivated default variant.
        var response = await admin.PostAsJsonAsync($"/api/v1/admin/flash-sales/{flashSale.Id}/items", new { variantId = product.Id, flashPrice = 10_000m, allocatedQuantity = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Promotions.FlashSaleVariantInactive");
    }

    [Fact]
    public async Task CreateGiftPromotion_Should_BeRejected_When_GiftVariantIsInactive()
    {
        var admin = await AdminClientAsync();
        var (product, _) = await CreateProductConvertedToOneActiveVariantAsync(admin);

        var response = await admin.PostAsJsonAsync("/api/v1/promotions", new
        {
            name = $"ZReviewB Gift {Guid.NewGuid():N}",
            description = (string?)null,
            type = "Gift",
            value = 0m,
            maxDiscountAmount = (decimal?)null,
            minOrderSubtotal = 100_000m,
            startsAt = DateTime.UtcNow.AddDays(-1),
            endsAt = DateTime.UtcNow.AddDays(30),
            usageLimit = (int?)null,
            perUserLimit = (int?)null,
            scope = "Order",
            targetProductIds = (List<Guid>?)null,
            targetCategoryIds = (List<Guid>?)null,
            minQuantity = (int?)null,
            // product.Id is also the id of the now-deactivated default variant.
            giftItems = new[] { new { productId = (Guid?)null, variantId = product.Id, quantity = 1 } }
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Promotions.GiftVariantInactive");
    }

    [Fact]
    public async Task PlaceOrder_Should_PriceLineAtActuallyReservedCampaign_When_CheapestOverlappingCampaignLacksQuota()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 100_000m);
        await AdjustStockAsync(admin, productId, 10);

        // Two overlapping active campaigns for the same product: the cheaper one can only cover 1 unit,
        // the pricier one can cover the full order. Reserve must fall through to the pricier campaign, and
        // the persisted order line must be priced at THAT campaign, not the (now-infeasible) cheaper one.
        var cheapSale = await CreateFlashSaleAsync(admin, $"ZReviewB Cheap {Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        var cheapItem = await AddFlashSaleItemAsync(admin, cheapSale.Id, productId, flashPrice: 40_000m, allocatedQuantity: 1);
        await ActivateFlashSaleAsync(admin, cheapSale.Id);

        var pricierSale = await CreateFlashSaleAsync(admin, $"ZReviewB Pricier {Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        var pricierItem = await AddFlashSaleItemAsync(admin, pricierSale.Id, productId, flashPrice: 70_000m, allocatedQuantity: 5);
        await ActivateFlashSaleAsync(admin, pricierSale.Id);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 2); // exceeds the cheap campaign's quota of 1

        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        placed.Lines.Should().ContainSingle(l => l.ProductId == productId && l.UnitPrice == 70_000m);
        placed.Subtotal.Should().Be(140_000m);

        var cheapReloaded = await GetFlashSaleAsync(admin, cheapSale.Id);
        var pricierReloaded = await GetFlashSaleAsync(admin, pricierSale.Id);
        cheapReloaded.Items.Single(i => i.Id == cheapItem.Id).SoldQuantity.Should().Be(0, "the under-quota campaign must not be touched");
        pricierReloaded.Items.Single(i => i.Id == pricierItem.Id).SoldQuantity.Should().Be(2);
    }
}

using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Infrastructure.Persistence;

namespace ZIONShop.Api.IntegrationTests.Variants;

/// <summary>
/// W21-W1-INVENTORY: InventoryItem/StockReservation/StockMovement are now keyed by VariantId (ProductId kept
/// as a plain reference column). A plain single-variant product's one variant id equals its product id (the
/// backfill rule), so these tests focus on the genuinely new behavior: a multi-variant product's SEPARATE
/// variants get separate rows/reservations, while the product-level aggregate endpoint sums across them.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class InventoryVariantsTests
{
    private readonly ApiFactory _factory;

    public InventoryVariantsTests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);

    private record InventoryItemData(Guid ProductId, Guid VariantId, string? VariantLabel, string Sku, string ProductName, int OnHand, int Reserved, int Available, int LowStockThreshold, bool IsLowStock);
    private record AvailabilityData(Guid ProductId, Guid VariantId, string? VariantLabel, int Available, bool InStock);
    private record ProductAvailabilityData(Guid ProductId, int Available, bool InStock);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(Guid ProductId, Guid VariantRed, Guid VariantBlue)> CreateTwoVariantProductAsync(HttpClient admin)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZInv Variant Widget {token}",
            slug = $"zinv-variant-widget-{token}",
            sku = $"ZIV-{token}",
            price = 100_000m,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for inventory variants",
            brand = "ZInvVariantBrand",
            images = Array.Empty<object>()
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var variantsBody = new
        {
            options = new[] { new { name = "Màu sắc", values = new[] { "Đỏ", "Xanh" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "Đỏ", option2Value = (string?)null, sku = $"ZIV-{token}-RED", price = 100_000m, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "Xanh", option2Value = (string?)null, sku = $"ZIV-{token}-BLUE", price = 100_000m, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        var setResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}/variants", variantsBody);
        setResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminGet = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        var adminProduct = await adminGet.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var variantRed = adminProduct!.Data!.Variants.Single(v => v.Option1Value == "Đỏ").Id;
        var variantBlue = adminProduct.Data.Variants.Single(v => v.Option1Value == "Xanh").Id;

        return (productId, variantRed, variantBlue);
    }

    private static async Task<InventoryItemData> AdjustVariantStockAsync(HttpClient admin, Guid variantId, int delta, string reason = "Variant integration test seed")
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/inventory/variants/{variantId}/adjust", new { delta, reason });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<InventoryItemData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<InventoryItemData> GetVariantAvailabilityRawAsync(HttpClient client, Guid variantId)
    {
        var response = await client.GetAsync($"/api/v1/inventory/variants/{variantId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AvailabilityData>>(ApiEnvelopeJson.Options);
        var a = envelope!.Data!;
        return new InventoryItemData(a.ProductId, a.VariantId, a.VariantLabel, string.Empty, string.Empty, 0, 0, a.Available, 0, false);
    }

    private static async Task<ProductAvailabilityData> GetProductAvailabilityAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/inventory/availability/products?productIds={productId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductAvailabilityData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!.Single(p => p.ProductId == productId);
    }

    [Fact]
    public async Task MultiVariantProduct_Should_HaveIndependentStockPerVariant_And_SumAtProductLevel()
    {
        var admin = await AdminClientAsync();
        var (productId, variantRed, variantBlue) = await CreateTwoVariantProductAsync(admin);

        var redAfterAdjust = await AdjustVariantStockAsync(admin, variantRed, 20);
        var blueAfterAdjust = await AdjustVariantStockAsync(admin, variantBlue, 30);

        redAfterAdjust.OnHand.Should().Be(20);
        redAfterAdjust.ProductId.Should().Be(productId);
        redAfterAdjust.VariantId.Should().Be(variantRed);
        blueAfterAdjust.OnHand.Should().Be(30);
        blueAfterAdjust.VariantId.Should().Be(variantBlue);

        // Each variant's availability is independent.
        var redAvailability = await GetVariantAvailabilityRawAsync(admin, variantRed);
        var blueAvailability = await GetVariantAvailabilityRawAsync(admin, variantBlue);
        redAvailability.Available.Should().Be(20);
        blueAvailability.Available.Should().Be(30);

        // Product-level aggregate sums across both variants.
        var productAvailability = await GetProductAvailabilityAsync(admin, productId);
        productAvailability.Available.Should().Be(50);
        productAvailability.InStock.Should().BeTrue();
    }

    /// <summary>
    /// SetVariants never deletes a replaced variant, only deactivates it (see Product.SetVariants) -- so a
    /// product that started as a single-default-variant product (Variant.Id == ProductId, same as
    /// IProductPriceLookup's convention) and was later given real option variants still has that original,
    /// now-inactive, Id==ProductId variant row forever. The product-id resolver therefore always finds a
    /// direct variant hit for such a product and never needs the "multiple active variants" ambiguity branch;
    /// that branch (InventoryErrors.VariantRequired) is covered directly against a mocked catalog in
    /// GetAvailabilityQueryHandlerTests/AdjustStockCommandHandlerTests instead, where a productId with no
    /// matching variant row at all can actually be constructed.
    /// </summary>
    [Fact]
    public async Task LegacyProductIdRoute_Should_ResolveToDeactivatedDefaultVariant_When_ProductWasLaterGivenRealVariants()
    {
        var admin = await AdminClientAsync();
        var (productId, _, _) = await CreateTwoVariantProductAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/inventory/products/{productId}/adjust", new { delta = 5, reason = "legacy route test" });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<InventoryItemData>>(ApiEnvelopeJson.Options);
        var adjusted = envelope!.Data!;

        adjusted.ProductId.Should().Be(productId);
        adjusted.VariantId.Should().Be(productId, "the original default variant's id always equals the product id");
        adjusted.OnHand.Should().Be(5);
    }

    [Fact]
    public async Task OrderWithBothVariants_Should_ReserveEachVariantIndependently()
    {
        var admin = await AdminClientAsync();
        var (productId, variantRed, variantBlue) = await CreateTwoVariantProductAsync(admin);
        await AdjustVariantStockAsync(admin, variantRed, 20);
        await AdjustVariantStockAsync(admin, variantBlue, 30);

        // Cart/Checkout variant support belongs to other W21 tracks; drive the Inventory reservation handler
        // directly with an OrderCreatedIntegrationEvent carrying two lines for the SAME product's two variants,
        // the same way Checkout/OversellTests.cs exercises the handler directly.
        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 500_000m,
            new[]
            {
                new OrderLine(productId, 2, 100_000m, false, variantRed),
                new OrderLine(productId, 3, 100_000m, false, variantBlue)
            },
            "COD", "VND");

        using (var scope = _factory.Services.CreateScope())
        {
            var handler = scope.ServiceProvider.GetServices<IIntegrationEventHandler<OrderCreatedIntegrationEvent>>()
                .Single(h => h.GetType().GetGenericArguments().Contains(typeof(ZIONShop.Inventory.Application.IntegrationEventHandlers.OrderCreatedIntegrationEventHandler)));
            await handler.HandleAsync(@event, CancellationToken.None);
        }

        var redAvailability = await GetVariantAvailabilityRawAsync(admin, variantRed);
        var blueAvailability = await GetVariantAvailabilityRawAsync(admin, variantBlue);
        redAvailability.Available.Should().Be(18, "2 units of the red variant were reserved for the order");
        blueAvailability.Available.Should().Be(27, "3 units of the blue variant were reserved for the order");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var reservations = await db.StockReservations.Where(r => r.OrderId == orderId).ToListAsync();
        reservations.Should().HaveCount(2);
        reservations.Should().ContainSingle(r => r.VariantId == variantRed && r.Quantity == 2 && r.ProductId == productId);
        reservations.Should().ContainSingle(r => r.VariantId == variantBlue && r.Quantity == 3 && r.ProductId == productId);

        var productAvailability = await GetProductAvailabilityAsync(admin, productId);
        productAvailability.Available.Should().Be(45, "sum of (20-2) red and (30-3) blue");
    }

    [Fact]
    public async Task Seeder_Should_CreateOneInventoryRow_PerVariant_ForNewMultiVariantProduct()
    {
        var admin = await AdminClientAsync();
        var (_, variantRed, variantBlue) = await CreateTwoVariantProductAsync(admin);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            var catalog = scope.ServiceProvider.GetRequiredService<IProductCatalogReader>();
            await ZIONShop.Inventory.Infrastructure.Persistence.InventoryDbSeeder.SeedAsync(db, catalog, CancellationToken.None);
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var variantIds = await verifyDb.InventoryItems
            .Where(i => i.VariantId == variantRed || i.VariantId == variantBlue)
            .Select(i => i.VariantId)
            .ToListAsync();

        variantIds.Should().BeEquivalentTo(new[] { variantRed, variantBlue });
    }
}

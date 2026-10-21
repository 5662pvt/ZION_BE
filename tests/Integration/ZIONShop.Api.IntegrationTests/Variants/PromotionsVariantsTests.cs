using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Promotions;
using static ZIONShop.Api.IntegrationTests.FlashSales.FlashSaleTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Variants;

/// <summary>
/// W21-W1-PROMOTIONS: FlashSaleItem/PromotionGiftItem are now keyed by VariantId (ProductId kept as a plain
/// reference column). These tests focus on the genuinely new behavior: a multi-variant product can have
/// flash-sale items for two DIFFERENT variants in the same campaign (previously impossible, since the old
/// unique index was (FlashSaleId, ProductId)), each with its own flash price/regular price, and reservations
/// against one variant must not touch the other variant's quota.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class PromotionsVariantsTests
{
    private readonly ApiFactory _factory;

    public PromotionsVariantsTests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(Guid ProductId, Guid VariantRed, Guid VariantBlue)> CreateTwoVariantProductAsync(HttpClient admin, decimal priceRed = 100_000m, decimal priceBlue = 150_000m)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZPromo Variant Widget {token}",
            slug = $"zpromo-variant-widget-{token}",
            sku = $"ZPV-{token}",
            price = priceRed,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for promotions variants",
            brand = "ZPromoVariantBrand",
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
                new { id = (Guid?)null, option1Value = "Đỏ", option2Value = (string?)null, sku = $"ZPV-{token}-RED", price = priceRed, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "Xanh", option2Value = (string?)null, sku = $"ZPV-{token}-BLUE", price = priceBlue, currency = "VND", isActive = true, imageId = (Guid?)null }
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

    [Fact]
    public async Task ActiveFlashSales_Should_ListBothVariants_WithOwnRegularPriceAndLabel_When_SameProductHasTwoFlashItems()
    {
        var admin = await AdminClientAsync();
        var (_, variantRed, variantBlue) = await CreateTwoVariantProductAsync(admin, priceRed: 100_000m, priceBlue: 150_000m);

        var flashSale = await CreateFlashSaleAsync(admin, $"ZTest Flash {Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        await AddFlashSaleItemByVariantAsync(admin, flashSale.Id, variantRed, flashPrice: 70_000m, allocatedQuantity: 5);
        await AddFlashSaleItemByVariantAsync(admin, flashSale.Id, variantBlue, flashPrice: 90_000m, allocatedQuantity: 3);
        await ActivateFlashSaleAsync(admin, flashSale.Id);

        var active = await GetActiveFlashSalesAsync(admin);
        var entry = active.Should().ContainSingle(f => f.Id == flashSale.Id).Subject;

        entry.Items.Should().HaveCount(2);
        var redItem = entry.Items.Single(i => i.VariantId == variantRed);
        var blueItem = entry.Items.Single(i => i.VariantId == variantBlue);
        redItem.FlashPrice.Should().Be(70_000m);
        redItem.RegularPrice.Should().Be(100_000m);
        blueItem.FlashPrice.Should().Be(90_000m);
        blueItem.RegularPrice.Should().Be(150_000m);
    }

    [Fact]
    public async Task ReserveAsync_Should_MoveOnlyThatVariantsQuota_When_FlashSaleHasTwoVariantsOfSameProduct()
    {
        var admin = await AdminClientAsync();
        var (productId, variantRed, variantBlue) = await CreateTwoVariantProductAsync(admin);

        var flashSale = await CreateFlashSaleAsync(admin, $"ZTest Flash {Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        var redItem = await AddFlashSaleItemByVariantAsync(admin, flashSale.Id, variantRed, flashPrice: 70_000m, allocatedQuantity: 5);
        await AddFlashSaleItemByVariantAsync(admin, flashSale.Id, variantBlue, flashPrice: 90_000m, allocatedQuantity: 5);
        await ActivateFlashSaleAsync(admin, flashSale.Id);

        using var scope = _factory.Services.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IFlashSaleEvaluator>();
        var quotes = await evaluator.ReserveAsync(new[] { new FlashSaleLine(productId, 2, variantRed) }, Guid.NewGuid(), Guid.NewGuid());

        quotes.Should().ContainSingle(q => q.VariantId == variantRed && q.FlashSaleItemId == redItem.Id);

        var reloaded = await GetFlashSaleAsync(admin, flashSale.Id);
        var redReloaded = reloaded.Items.Single(i => i.VariantId == variantRed);
        var blueReloaded = reloaded.Items.Single(i => i.VariantId == variantBlue);
        redReloaded.SoldQuantity.Should().Be(2);
        blueReloaded.SoldQuantity.Should().Be(0, "only the red variant's quota should move");
    }
}

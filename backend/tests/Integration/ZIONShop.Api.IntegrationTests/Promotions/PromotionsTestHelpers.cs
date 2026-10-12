using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Promotions;

public record PromotionData(
    Guid Id, string Name, string? Description, string Type, decimal Value, decimal? MaxDiscountAmount, decimal MinOrderSubtotal,
    DateTime StartsAt, DateTime EndsAt, bool IsActive, string Status, int? UsageLimit, int? PerUserLimit,
    int UsedCount, int CouponCount, string Scope, List<Guid> TargetProductIds, List<Guid> TargetCategoryIds, List<CouponData> Coupons);

public record CouponData(Guid Id, string Code, bool IsActive, int UsedCount, DateTime CreatedAt);

public record CouponValidationData(string Code, string PromotionName, decimal DiscountAmount, string DiscountTarget);

public record CategoryData(Guid Id, string Name, string Slug, string? Description, Guid? ParentId, int DisplayOrder);

public record ScopedProductData(Guid Id);

public static class PromotionsTestHelpers
{
    public static async Task<HttpResponseMessage> CreatePromotionRawAsync(
        HttpClient admin,
        string type = "PercentageOff",
        decimal value = 10m,
        decimal? maxDiscountAmount = null,
        decimal minOrderSubtotal = 0m,
        int? usageLimit = null,
        int? perUserLimit = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        string scope = "Order",
        IReadOnlyList<Guid>? targetProductIds = null,
        IReadOnlyList<Guid>? targetCategoryIds = null)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        return await admin.PostAsJsonAsync("/api/v1/promotions", new
        {
            name = $"ZTest Promotion {token}",
            description = (string?)null,
            type,
            value,
            maxDiscountAmount,
            minOrderSubtotal,
            startsAt = startsAt ?? DateTime.UtcNow.AddDays(-1),
            endsAt = endsAt ?? DateTime.UtcNow.AddDays(30),
            usageLimit,
            perUserLimit,
            scope,
            targetProductIds,
            targetCategoryIds
        });
    }

    public static async Task<PromotionData> CreatePromotionAsync(
        HttpClient admin,
        string type = "PercentageOff",
        decimal value = 10m,
        decimal? maxDiscountAmount = null,
        decimal minOrderSubtotal = 0m,
        int? usageLimit = null,
        int? perUserLimit = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        string scope = "Order",
        IReadOnlyList<Guid>? targetProductIds = null,
        IReadOnlyList<Guid>? targetCategoryIds = null)
    {
        var response = await CreatePromotionRawAsync(
            admin, type, value, maxDiscountAmount, minOrderSubtotal, usageLimit, perUserLimit, startsAt, endsAt,
            scope, targetProductIds, targetCategoryIds);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PromotionData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<PromotionData> ActivatePromotionAsync(HttpClient admin, Guid promotionId)
    {
        var response = await admin.PostAsync($"/api/v1/promotions/{promotionId}/activate", null);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PromotionData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> CreateCouponRawAsync(HttpClient admin, Guid promotionId, string code)
        => await admin.PostAsJsonAsync($"/api/v1/promotions/{promotionId}/coupons", new { code });

    public static async Task<CouponData> CreateCouponAsync(HttpClient admin, Guid promotionId, string code)
    {
        var response = await CreateCouponRawAsync(admin, promotionId, code);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CouponData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<List<CouponData>> GenerateCouponsAsync(HttpClient admin, Guid promotionId, string prefix, int count)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/promotions/{promotionId}/coupons/generate", new { prefix, count });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CouponData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> ValidateCouponRawAsync(HttpClient customer, string code)
        => await customer.PostAsJsonAsync("/api/v1/promotions/coupons/validate", new { code });

    public static async Task<CouponValidationData> ValidateCouponAsync(HttpClient customer, string code)
    {
        var response = await ValidateCouponRawAsync(customer, code);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CouponValidationData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<PromotionData> GetPromotionByIdAsync(HttpClient admin, Guid promotionId)
    {
        var response = await admin.GetAsync($"/api/v1/promotions/{promotionId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PromotionData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>
    /// Coupon usage is reserved/released by <c>IPromotionEvaluator</c> inline with the HTTP request for
    /// reservation, but release on order cancellation runs off the transactional outbox (same as order
    /// status transitions), so UsedCount only settles to its post-cancel value asynchronously.
    /// </summary>
    public static async Task<PromotionData> WaitForPromotionUsedCountAsync(HttpClient admin, Guid promotionId, int expectedUsedCount, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        PromotionData last;
        do
        {
            last = await GetPromotionByIdAsync(admin, promotionId);
            if (last.UsedCount == expectedUsedCount) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Promotion {promotionId} did not reach UsedCount {expectedUsedCount} within {timeout}. Last observed: {last.UsedCount}.");
    }

    public static async Task<Guid> CreateCategoryAsync(HttpClient admin)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var response = await admin.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"ZTest Category {token}",
            slug = $"ztest-category-{token}",
            parentId = (Guid?)null,
            description = (string?)null,
            displayOrder = 0
        });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CategoryData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!.Id;
    }

    public static async Task<Guid> CreatePublishedProductInCategoryAsync(HttpClient admin, Guid? categoryId, decimal price)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZTest Scoped Product {token}",
            slug = $"ztest-scoped-product-{token}",
            sku = $"ZT-SCP-{token.ToUpperInvariant()}",
            price,
            currency = "VND",
            categoryId,
            description = "integration test product for promotion scoping",
            brand = "ZTestBrand",
            images = Array.Empty<object>()
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ScopedProductData>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        return productId;
    }

    public static async Task<(Guid PromotionId, string Code)> SeedScopedPromotionAsync(
        HttpClient admin,
        string code,
        string type,
        decimal value,
        string scope,
        IReadOnlyList<Guid>? targetProductIds = null,
        IReadOnlyList<Guid>? targetCategoryIds = null,
        decimal? maxDiscountAmount = null,
        decimal minOrderSubtotal = 0m)
    {
        var promotion = await CreatePromotionAsync(
            admin, type, value, maxDiscountAmount, minOrderSubtotal,
            scope: scope, targetProductIds: targetProductIds, targetCategoryIds: targetCategoryIds);
        await ActivatePromotionAsync(admin, promotion.Id);
        var coupon = await CreateCouponAsync(admin, promotion.Id, code);
        return (promotion.Id, coupon.Code);
    }
}

using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.FlashSales;

public record FlashSaleItemData(Guid Id, Guid ProductId, decimal FlashPrice, int AllocatedQuantity, int SoldQuantity, int Available);
public record FlashSaleData(Guid Id, string Name, DateTime StartsAt, DateTime EndsAt, bool IsActive, List<FlashSaleItemData> Items);
public record ActiveFlashSaleItemData(Guid ProductId, string ProductName, string? ImageUrl, decimal FlashPrice, decimal RegularPrice, int Available, int AllocatedQuantity, DateTime EndsAt);
public record ActiveFlashSaleData(Guid Id, string Name, DateTime StartsAt, DateTime EndsAt, List<ActiveFlashSaleItemData> Items);

public static class FlashSaleTestHelpers
{
    public static async Task<FlashSaleData> CreateFlashSaleAsync(HttpClient admin, string name, DateTime startsAt, DateTime endsAt)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/flash-sales", new { name, startsAt, endsAt });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<FlashSaleData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<FlashSaleItemData> AddFlashSaleItemAsync(HttpClient admin, Guid flashSaleId, Guid productId, decimal flashPrice, int allocatedQuantity)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/admin/flash-sales/{flashSaleId}/items", new { productId, flashPrice, allocatedQuantity });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<FlashSaleItemData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task ActivateFlashSaleAsync(HttpClient admin, Guid flashSaleId)
    {
        var response = await admin.PostAsync($"/api/v1/admin/flash-sales/{flashSaleId}/activate", null);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<FlashSaleData> GetFlashSaleAsync(HttpClient admin, Guid flashSaleId)
    {
        var response = await admin.GetAsync($"/api/v1/admin/flash-sales/{flashSaleId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<FlashSaleData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<List<ActiveFlashSaleData>> GetActiveFlashSalesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/flash-sales/active");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ActiveFlashSaleData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>Creates an already-active flash sale with a single item, window spanning now.</summary>
    public static async Task<(FlashSaleData FlashSale, FlashSaleItemData Item)> CreateActiveFlashSaleWithItemAsync(
        HttpClient admin, Guid productId, decimal flashPrice, int allocatedQuantity)
    {
        var flashSale = await CreateFlashSaleAsync(admin, $"ZTest Flash {Guid.NewGuid():N}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(1));
        var item = await AddFlashSaleItemAsync(admin, flashSale.Id, productId, flashPrice, allocatedQuantity);
        await ActivateFlashSaleAsync(admin, flashSale.Id);
        return (flashSale, item);
    }
}

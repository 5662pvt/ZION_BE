using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Checkout;

public record ProductFull(Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status, Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images);
public record AddressData(Guid Id, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress, bool IsDefault);
public record AvailabilityData(Guid ProductId, int Available, bool InStock);
public record InventoryItemData(Guid ProductId, string Sku, string ProductName, int OnHand, int Reserved, int Available, int LowStockThreshold, bool IsLowStock);
public record ShippingAddressData(Guid AddressId, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress);
public record OrderLineData(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal, bool IsGift);
public record OrderStatusHistoryData(string Status, DateTime At, string? Note);
public record OrderData(Guid Id, string Code, string Status, string PaymentMethod, decimal Subtotal, decimal ShippingFee, string? CouponCode, decimal DiscountAmount, string? DiscountTarget, decimal Total, string Currency, DateTime CreatedAt, string? Note, string? CancelReason, ShippingAddressData ShippingAddress, List<OrderLineData> Lines, List<OrderStatusHistoryData> History);
public record AdminOrderSummaryData(Guid Id, string Code, string Status, string PaymentMethod, decimal Total, string Currency, DateTime CreatedAt, int ItemCount, string RecipientName, string PhoneNumber);
public record CartItemData(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal Subtotal, string Currency);
public record CartData(Guid Id, Guid? UserId, string? AnonymousId, decimal Subtotal, string Currency, int TotalQuantity, List<CartItemData> Items);

public static class CheckoutTestHelpers
{
    public static async Task<(Guid ProductId, string Sku)> CreatePublishedProductAsync(HttpClient admin, decimal price = 100000m)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var sku = $"ZT-CHK-{token.ToUpperInvariant()}";
        var createResponse = await admin.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"ZTest Checkout Product {token}",
            slug = $"ztest-checkout-product-{token}",
            sku,
            price,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for checkout",
            brand = "ZTestBrand",
            images = Array.Empty<object>()
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        return (productId, sku);
    }

    public static async Task<InventoryItemData> AdjustStockAsync(HttpClient admin, Guid productId, int delta, string reason = "Integration test seed")
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/inventory/products/{productId}/adjust", new { delta, reason });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<InventoryItemData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<AvailabilityData> GetAvailabilityAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/inventory/products/{productId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AvailabilityData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<InventoryItemData> GetInventoryItemBySkuAsync(HttpClient admin, string sku)
    {
        var response = await admin.GetAsync($"/api/v1/inventory?keyword={sku}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<InventoryItemData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!.Single(i => i.Sku == sku);
    }

    public static async Task<(HttpClient Client, Guid AddressId, Guid UserId)> CreateCustomerWithAddressAsync(ApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"customer-{Guid.NewGuid():N}@zionshop.local";
        const string password = "Customer@123";

        await client.RegisterAndConfirmAsync(factory, email, password, "Test Customer");

        var loginResponse = await client.LoginAsync(email, password);
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        var login = loginEnvelope!.Data!;
        client.WithBearerToken(login.AccessToken);

        var addressResponse = await client.PostAsJsonAsync("/api/v1/users/me/addresses", new
        {
            recipientName = "Nguyen Van Test",
            phoneNumber = "0901234567",
            province = "TP.HCM",
            ward = "Phuong Ben Nghe",
            streetAddress = "123 Test St",
            isDefault = true
        });
        addressResponse.EnsureSuccessStatusCode();
        var addressEnvelope = await addressResponse.Content.ReadFromJsonAsync<ApiEnvelope<AddressData>>(ApiEnvelopeJson.Options);

        return (client, addressEnvelope!.Data!.Id, login.UserId);
    }

    public static async Task AddToCartAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { productId, quantity });
        response.EnsureSuccessStatusCode();
    }

    public static async Task<CartData> GetCartAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/cart");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CartData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> PlaceOrderRawAsync(HttpClient client, Guid addressId, string paymentMethod, string? note = null, string? couponCode = null)
        => await client.PostAsJsonAsync("/api/v1/orders", new { addressId, paymentMethod, note, couponCode });

    public static async Task<OrderData> PlaceOrderAsync(HttpClient client, Guid addressId, string paymentMethod, string? note = null, string? couponCode = null)
    {
        var response = await PlaceOrderRawAsync(client, addressId, paymentMethod, note, couponCode);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> GetOrderRawAsync(HttpClient client, Guid orderId)
        => await client.GetAsync($"/api/v1/orders/{orderId}");

    public static async Task<List<AdminOrderSummaryData>> AdminSearchOrdersAsync(
        HttpClient admin, string? keyword = null, string? status = null, DateTime? from = null, DateTime? to = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(keyword)) query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (from.HasValue) query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        if (to.HasValue) query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

        var response = await admin.GetAsync($"/api/v1/orders{qs}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<AdminOrderSummaryData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<List<AvailabilityData>> GetAvailabilityBatchAsync(HttpClient client, IEnumerable<Guid> productIds)
    {
        var qs = string.Join(",", productIds);
        var response = await client.GetAsync($"/api/v1/inventory/availability?productIds={qs}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<AvailabilityData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> CancelOrderRawAsync(HttpClient client, Guid orderId, string? reason = null)
        => await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/cancel", new { reason });

    public static async Task<OrderData> GetOrderAsync(HttpClient client, Guid orderId)
    {
        var response = await GetOrderRawAsync(client, orderId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>
    /// Integration events (OrderCreated → InventoryReserved/Failed → order status transition) flow through
    /// the transactional outbox, which OutboxPublisherService polls on its own interval (RabbitMq:OutboxPollSeconds,
    /// default 2s) rather than being applied inline with the HTTP request — so the order status settles
    /// asynchronously after PlaceOrder/Cancel/Ship/Complete returns.
    /// </summary>
    public static async Task<OrderData> WaitForOrderStatusAsync(HttpClient client, Guid orderId, string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        OrderData last;
        do
        {
            last = await GetOrderAsync(client, orderId);
            if (last.Status == expectedStatus) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Order {orderId} did not reach status '{expectedStatus}' within {timeout}. Last observed status: '{last.Status}'.");
    }

    /// <summary>
    /// Stock mutations that follow an order status transition (reservation commit on OrderConfirmed,
    /// release on OrderCancelled) are applied by the Inventory module only after the outbox delivers
    /// that event — i.e. after the HTTP response/poll already reported the new order status. Any stock
    /// assertion made right after observing such a status must poll until the expected inventory state
    /// is reached rather than reading once.
    /// </summary>
    public static async Task<InventoryItemData> WaitForInventoryAsync(
        HttpClient admin,
        string sku,
        Func<InventoryItemData, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        InventoryItemData last;
        do
        {
            last = await GetInventoryItemBySkuAsync(admin, sku);
            if (predicate(last)) return last;
            await Task.Delay(250);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Inventory for SKU '{sku}' did not reach the expected state within {timeout}. Last observed: OnHand={last.OnHand}, Reserved={last.Reserved}.");
    }

    /// <summary>
    /// Same rationale as <see cref="WaitForInventoryAsync"/>, but keyed by productId against the public
    /// availability endpoint -- convenient for gift-line assertions where only the productId (not the
    /// SKU) is at hand.
    /// </summary>
    public static async Task<AvailabilityData> WaitForAvailabilityAsync(
        HttpClient client,
        Guid productId,
        Func<AvailabilityData, bool> predicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        AvailabilityData last;
        do
        {
            last = await GetAvailabilityAsync(client, productId);
            if (predicate(last)) return last;
            await Task.Delay(250);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Availability for product '{productId}' did not reach the expected state within {timeout}. Last observed: Available={last.Available}.");
    }
}

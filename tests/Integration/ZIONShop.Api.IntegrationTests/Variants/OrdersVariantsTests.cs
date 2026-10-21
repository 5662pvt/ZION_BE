using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Variants;

/// <summary>
/// W21-W1-ORDERS: OrderLine now carries a VariantId/VariantLabel/Sku snapshot, keyed by
/// VariantId ?? ProductId for pricing/stock. This exercises the demo-style multi-variant
/// case end to end: two variants of the same product, priced and stocked independently,
/// placed in one COD order.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class OrdersVariantsTests
{
    private readonly ApiFactory _factory;

    public OrdersVariantsTests(ApiFactory factory) => _factory = factory;

    private record ProductVariant(Guid Id, string Sku, decimal Price, string Currency, string? Option1Value, string? Option2Value, string Label, Guid? ImageId, string? ImageUrl, bool IsActive);
    private record ProductOption(string Name, List<string> Values);
    private record ProductFull(
        Guid Id, string Name, string Slug, string? Description, string Sku, decimal Price, string Currency, string Status,
        Guid? CategoryId, string? CategoryName, string? Brand, List<object> Images,
        List<ProductOption> Options, List<ProductVariant> Variants, decimal MinPrice, decimal MaxPrice);

    private record AddressData(Guid Id, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress, bool IsDefault);
    private record ShippingAddressData(Guid AddressId, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress);
    private record OrderLineData(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal, bool IsGift, Guid VariantId, string? VariantLabel, string? Sku);
    private record OrderStatusHistoryData(string Status, DateTime At, string? Note);
    private record OrderData(Guid Id, string Code, string Status, string PaymentMethod, decimal Subtotal, decimal ShippingFee, string? CouponCode, decimal DiscountAmount, string? DiscountTarget, decimal Total, string Currency, DateTime CreatedAt, string? Note, string? CancelReason, ShippingAddressData ShippingAddress, List<OrderLineData> Lines, List<OrderStatusHistoryData> History);

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
            name = $"ZOrd Variant Widget {token}",
            slug = $"zord-variant-widget-{token}",
            sku = $"ZOV-{token}",
            price = priceRed,
            currency = "VND",
            categoryId = (Guid?)null,
            description = "integration test product for order variants",
            brand = "ZOrdVariantBrand",
            images = Array.Empty<object>()
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var productId = created!.Data!.Id;

        var variantsBody = new
        {
            options = new[] { new { name = "Màu sắc", values = new[] { "Đỏ", "Xanh" } } },
            variants = new object[]
            {
                new { id = (Guid?)null, option1Value = "Đỏ", option2Value = (string?)null, sku = $"ZOV-{token}-RED", price = priceRed, currency = "VND", isActive = true, imageId = (Guid?)null },
                new { id = (Guid?)null, option1Value = "Xanh", option2Value = (string?)null, sku = $"ZOV-{token}-BLUE", price = priceBlue, currency = "VND", isActive = true, imageId = (Guid?)null }
            }
        };
        var setResponse = await admin.PutAsJsonAsync($"/api/v1/products/{productId}/variants", variantsBody);
        setResponse.EnsureSuccessStatusCode();

        var publishResponse = await admin.PostAsync($"/api/v1/products/{productId}/publish", null);
        publishResponse.EnsureSuccessStatusCode();

        var adminGet = await admin.GetAsync($"/api/v1/products/admin/{productId}");
        var adminProduct = await adminGet.Content.ReadFromJsonAsync<ApiEnvelope<ProductFull>>(ApiEnvelopeJson.Options);
        var variantRed = adminProduct!.Data!.Variants.Single(v => v.Option1Value == "Đỏ" && v.IsActive).Id;
        var variantBlue = adminProduct.Data.Variants.Single(v => v.Option1Value == "Xanh" && v.IsActive).Id;

        return (productId, variantRed, variantBlue);
    }

    private static async Task AdjustVariantStockAsync(HttpClient admin, Guid variantId, int delta)
    {
        var response = await admin.PostAsJsonAsync($"/api/v1/inventory/variants/{variantId}/adjust", new { delta, reason = "Order variant integration test seed" });
        response.EnsureSuccessStatusCode();
    }

    private async Task<(HttpClient Client, Guid AddressId)> CreateLoggedInCustomerWithAddressAsync()
    {
        var client = _factory.CreateClient();
        var email = $"order-variant-{Guid.NewGuid():N}@zionshop.local";
        const string password = "Customer@123";
        await client.RegisterAndConfirmAsync(_factory, email, password, "Order Variant Tester");

        var loginResponse = await client.LoginAsync(email, password);
        loginResponse.EnsureSuccessStatusCode();
        var loginEnvelope = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        client.WithBearerToken(loginEnvelope!.Data!.AccessToken);

        var addressResponse = await client.PostAsJsonAsync("/api/v1/users/me/addresses", new
        {
            recipientName = "Nguyen Van Order Variant",
            phoneNumber = "0909999999",
            province = "Thành phố Hà Nội",
            ward = "Phường Ba Đình",
            streetAddress = "456 Order Variant St",
            isDefault = true
        });
        addressResponse.EnsureSuccessStatusCode();
        var addressEnvelope = await addressResponse.Content.ReadFromJsonAsync<ApiEnvelope<AddressData>>(ApiEnvelopeJson.Options);

        return (client, addressEnvelope!.Data!.Id);
    }

    private static async Task AddVariantToCartAsync(HttpClient client, Guid variantId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { productId = (Guid?)null, variantId, quantity });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<OrderData> PlaceOrderAsync(HttpClient client, Guid addressId, string paymentMethod)
    {
        var response = await client.PostAsJsonAsync("/api/v1/orders", new { addressId, paymentMethod, note = (string?)null, couponCode = (string?)null });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<OrderData> GetOrderAsync(HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/v1/orders/{orderId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>Same rationale as CheckoutTestHelpers.WaitForOrderStatusAsync: the order status settles
    /// asynchronously via the outbox, after the HTTP response already came back.</summary>
    private static async Task<OrderData> WaitForOrderStatusAsync(HttpClient client, Guid orderId, string expectedStatus, TimeSpan? timeout = null)
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

    [Fact]
    public async Task PlaceOrder_Should_CreateTwoLines_WithDistinctVariantIdsLabelsAndPrices_When_CartHasTwoVariantsOfSameProduct()
    {
        var admin = await AdminClientAsync();
        var (productId, variantRed, variantBlue) = await CreateTwoVariantProductAsync(admin, priceRed: 100_000m, priceBlue: 150_000m);
        await AdjustVariantStockAsync(admin, variantRed, 10);
        await AdjustVariantStockAsync(admin, variantBlue, 10);

        var (customer, addressId) = await CreateLoggedInCustomerWithAddressAsync();
        await AddVariantToCartAsync(customer, variantRed, 2);
        await AddVariantToCartAsync(customer, variantBlue, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        placed.Lines.Should().HaveCount(2);
        placed.Lines.Should().ContainSingle(l => l.ProductId == productId && l.VariantId == variantRed && l.VariantLabel == "Đỏ" && l.UnitPrice == 100_000m && l.Quantity == 2);
        placed.Lines.Should().ContainSingle(l => l.ProductId == productId && l.VariantId == variantBlue && l.VariantLabel == "Xanh" && l.UnitPrice == 150_000m && l.Quantity == 1);
        placed.Subtotal.Should().Be(100_000m * 2 + 150_000m);

        var confirmed = await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");
        confirmed.Lines.Should().ContainSingle(l => l.VariantId == variantRed && l.VariantLabel == "Đỏ");
        confirmed.Lines.Should().ContainSingle(l => l.VariantId == variantBlue && l.VariantLabel == "Xanh");

        var detail = await GetOrderAsync(customer, placed.Id);
        detail.Lines.Should().ContainSingle(l => l.VariantId == variantRed && l.Sku != null && l.Sku.EndsWith("-RED"));
        detail.Lines.Should().ContainSingle(l => l.VariantId == variantBlue && l.Sku != null && l.Sku.EndsWith("-BLUE"));
    }
}

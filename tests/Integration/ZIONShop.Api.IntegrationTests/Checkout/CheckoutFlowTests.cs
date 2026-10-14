using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Checkout;

[Collection(ApiTestCollection.Name)]
public class CheckoutFlowTests
{
    private readonly ApiFactory _factory;

    public CheckoutFlowTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task Cod_Should_ConfirmOrder_CommitStock_And_ClearCart_When_TwoLinesOrdered()
    {
        var admin = await AdminClientAsync();
        var (productA, skuA) = await CreatePublishedProductAsync(admin, price: 100000m);
        var (productB, skuB) = await CreatePublishedProductAsync(admin, price: 100000m);
        await AdjustStockAsync(admin, productA, 10);
        await AdjustStockAsync(admin, productB, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productA, 2);
        await AddToCartAsync(customer, productB, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        var confirmed = await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        confirmed.Subtotal.Should().Be(300000m);
        confirmed.ShippingFee.Should().Be(30000m);
        confirmed.Total.Should().Be(330000m);

        var availabilityA = await GetAvailabilityAsync(customer, productA);
        availabilityA.Available.Should().Be(8);
        var availabilityB = await GetAvailabilityAsync(customer, productB);
        availabilityB.Available.Should().Be(9);

        var itemA = await WaitForInventoryAsync(admin, skuA, i => i.OnHand == 8 && i.Reserved == 0);
        itemA.OnHand.Should().Be(8);
        itemA.Reserved.Should().Be(0);

        var cart = await GetCartAsync(customer);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Cod_Should_WaiveShippingFee_When_SubtotalMeetsFreeShippingThreshold()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 600000m);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        var confirmed = await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        confirmed.Subtotal.Should().Be(600000m);
        confirmed.ShippingFee.Should().Be(0m);
        confirmed.Total.Should().Be(600000m);
    }

    [Fact]
    public async Task PlaceOrder_Should_Return409InsufficientStock_And_LeaveStockUnchanged_When_RequestedQuantityExceedsOnHand()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 1);
        var productName = await GetProductNameAsync(admin, productId);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 2);
        var ordersBefore = await GetMyOrdersCountAsync(customer);

        var response = await PlaceOrderRawAsync(customer, addressId, "COD");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Orders.InsufficientStock" && e.Message!.Contains(productName));

        (await GetMyOrdersCountAsync(customer)).Should().Be(ordersBefore);

        var item = await GetInventoryItemBySkuAsync(admin, sku);
        item.OnHand.Should().Be(1);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task CustomerCancel_Should_ReturnCommittedStockToOnHand_When_ConfirmedCodOrderIsCancelled()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 3);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var itemAfterConfirm = await WaitForInventoryAsync(admin, sku, i => i.OnHand == 7 && i.Reserved == 0);
        itemAfterConfirm.OnHand.Should().Be(7);
        itemAfterConfirm.Reserved.Should().Be(0);

        var cancelResponse = await CancelOrderRawAsync(customer, placed.Id);
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelled = await WaitForOrderStatusAsync(customer, placed.Id, "Cancelled");
        cancelled.CancelReason.Should().NotBeNullOrWhiteSpace();

        var itemAfterCancel = await WaitForInventoryAsync(admin, sku, i => i.OnHand == 10 && i.Reserved == 0);
        itemAfterCancel.OnHand.Should().Be(10);
        itemAfterCancel.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task AdminShipAndComplete_Should_AdvanceOrder_And_Reject_CustomerCancelAfterShipped()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null);
        shipResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetOrderAsync(customer, placed.Id)).Status.Should().Be("Shipped");

        var cancelAttempt = await CancelOrderRawAsync(customer, placed.Id);
        ((int)cancelAttempt.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);

        var completeResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetOrderAsync(customer, placed.Id)).Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Online_Should_StayAwaitingPayment_With_ActiveReservation_When_NoPaymentMade()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 3);

        var placed = await PlaceOrderAsync(customer, addressId, "Online");
        await WaitForOrderStatusAsync(customer, placed.Id, "AwaitingPayment");

        var availability = await GetAvailabilityAsync(customer, productId);
        availability.Available.Should().Be(7);

        var item = await GetInventoryItemBySkuAsync(admin, sku);
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(3);
    }

    [Fact]
    public async Task Concurrency_Should_ConfirmExactlyOneOrder_When_TwoCustomersOrderTheLastUnit()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 1);

        var (customerA, addressA, _) = await CreateCustomerWithAddressAsync(_factory);
        var (customerB, addressB, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customerA, productId, 1);
        await AddToCartAsync(customerB, productId, 1);

        var placeTasks = await Task.WhenAll(
            PlaceOrderAsync(customerA, addressA, "COD"),
            PlaceOrderAsync(customerB, addressB, "COD"));
        var orderA = placeTasks[0];
        var orderB = placeTasks[1];

        var statusA = await WaitForTerminalStatusAsync(customerA, orderA.Id);
        var statusB = await WaitForTerminalStatusAsync(customerB, orderB.Id);

        var statuses = new[] { statusA, statusB };
        statuses.Should().Contain("Confirmed").And.Contain("Cancelled");

        var item = await WaitForInventoryAsync(admin, sku, i => i.OnHand == 0 && i.Reserved == 0);
        item.OnHand.Should().Be(0);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public async Task GetOrder_Should_Return404_ForOtherCustomer_But_200_ForAdmin()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customerA, addressA, _) = await CreateCustomerWithAddressAsync(_factory);
        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customerA, productId, 1);

        var placed = await PlaceOrderAsync(customerA, addressA, "COD");

        var otherCustomerResponse = await GetOrderRawAsync(customerB, placed.Id);
        otherCustomerResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var adminResponse = await GetOrderRawAsync(admin, placed.Id);
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> WaitForTerminalStatusAsync(HttpClient client, Guid orderId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        OrderData last;
        do
        {
            last = await GetOrderAsync(client, orderId);
            if (last.Status is "Confirmed" or "Cancelled") return last.Status;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Order {orderId} did not reach a terminal status within {timeout}. Last observed status: '{last.Status}'.");
    }

    private sealed record ProductNameData(string Name);

    private static async Task<string> GetProductNameAsync(HttpClient admin, Guid productId)
    {
        var response = await admin.GetAsync($"/api/v1/products/{productId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ProductNameData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!.Name;
    }

    private static async Task<int> GetMyOrdersCountAsync(HttpClient customer)
    {
        var response = await customer.GetAsync("/api/v1/orders/mine?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<object>>>(ApiEnvelopeJson.Options);
        return envelope!.Pagination!.TotalCount;
    }
}

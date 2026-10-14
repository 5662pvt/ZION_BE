using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Checkout;

/// <summary>
/// Stresses the atomic conditional-UPDATE reservation path (<c>IInventoryItemRepository.TryReserveAsync</c>)
/// added to replace the old "load item, Reserve(), save with xmin, retry 3x in a fresh scope" handler --
/// under real contention that approach either oversold or produced false "Hết hàng" cancellations while
/// stock was still available. These tests assert the new behavior never oversells, never goes negative,
/// and never deadlocks when two orders touch the same two products in opposite order.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class OversellTests
{
    private readonly ApiFactory _factory;

    public OversellTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task FiftyConcurrentSingleUnitOrders_Should_ReserveExactlyTen_When_OnHandIsTen()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        const int customerCount = 50;
        var customers = new List<(HttpClient Client, Guid AddressId)>();
        for (var i = 0; i < customerCount; i++)
        {
            var (client, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
            await AddToCartAsync(client, productId, 1);
            customers.Add((client, addressId));
        }

        var responses = await Task.WhenAll(customers.Select(c => PlaceOrderRawAsync(c.Client, c.AddressId, "COD")));

        foreach (var response in responses)
        {
            var status = (int)response.StatusCode;
            (status is >= 200 and < 300 || status == (int)HttpStatusCode.Conflict)
                .Should().BeTrue($"expected 2xx or 409, got {status}");
        }

        var createdOrders = new List<(HttpClient Client, Guid OrderId)>();
        for (var i = 0; i < responses.Length; i++)
        {
            var status = (int)responses[i].StatusCode;
            if (status is >= 200 and < 300)
            {
                var envelope = await responses[i].Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
                createdOrders.Add((customers[i].Client, envelope!.Data!.Id));
            }
        }

        var finalStates = await WaitUntilNoLongerPendingAsync(createdOrders);

        var nonCancelled = finalStates.Count(o => o.Status != "Cancelled");
        nonCancelled.Should().Be(10, "exactly 10 of the 50 single-unit requests can be satisfied by 10 units of stock");

        foreach (var cancelled in finalStates.Where(o => o.Status == "Cancelled"))
            cancelled.CancelReason.Should().Contain("Hết hàng");

        // COD orders commit (OnHand -= reserved, Reserved -> 0) automatically once Confirmed, so once every
        // order has settled the item should be fully, exactly depleted -- never oversold, never negative.
        var item = await WaitForInventoryAsync(admin, sku, i => i.Reserved == 0, TimeSpan.FromSeconds(60));
        item.OnHand.Should().Be(0);
        item.Reserved.Should().Be(0);
        item.Available.Should().Be(0);
    }

    [Fact]
    public async Task TwoCustomers_MultiProductOppositeOrder_Should_NotDeadlock_Across_TenConcurrentRounds()
    {
        var admin = await AdminClientAsync();
        var (productA, skuA) = await CreatePublishedProductAsync(admin);
        var (productB, skuB) = await CreatePublishedProductAsync(admin);
        const int initialStock = 1000;
        await AdjustStockAsync(admin, productA, initialStock);
        await AdjustStockAsync(admin, productB, initialStock);

        const int rounds = 10;
        var allOrders = new List<(HttpClient Client, Guid OrderId)>();

        for (var round = 0; round < rounds; round++)
        {
            var (customer1, address1, _) = await CreateCustomerWithAddressAsync(_factory);
            var (customer2, address2, _) = await CreateCustomerWithAddressAsync(_factory);

            // customer1 touches A then B; customer2 touches B then A -- opposite order, same two rows.
            await AddToCartAsync(customer1, productA, 1);
            await AddToCartAsync(customer1, productB, 1);
            await AddToCartAsync(customer2, productB, 1);
            await AddToCartAsync(customer2, productA, 1);

            var responses = await Task.WhenAll(
                PlaceOrderRawAsync(customer1, address1, "COD"),
                PlaceOrderRawAsync(customer2, address2, "COD"));

            foreach (var response in responses)
            {
                var status = (int)response.StatusCode;
                (status is >= 200 and < 300 || status == (int)HttpStatusCode.Conflict)
                    .Should().BeTrue($"no deadlock/5xx expected in round {round}, got {status}");
            }

            var roundClients = new[] { customer1, customer2 };
            for (var i = 0; i < responses.Length; i++)
            {
                var status = (int)responses[i].StatusCode;
                if (status is >= 200 and < 300)
                {
                    var envelope = await responses[i].Content.ReadFromJsonAsync<ApiEnvelope<OrderData>>(ApiEnvelopeJson.Options);
                    allOrders.Add((roundClients[i], envelope!.Data!.Id));
                }
            }
        }

        // A deadlock would surface here as a timeout (order stuck in PendingStock) or a thrown exception.
        var finalStates = await WaitUntilNoLongerPendingAsync(allOrders);
        var confirmedCount = finalStates.Count(o => o.Status == "Confirmed");
        confirmedCount.Should().Be(allOrders.Count, "stock is abundant (1000) relative to the 20 orders placed, so none should be cancelled");

        var itemA = await WaitForInventoryAsync(admin, skuA, i => i.Reserved == 0, TimeSpan.FromSeconds(60));
        var itemB = await WaitForInventoryAsync(admin, skuB, i => i.Reserved == 0, TimeSpan.FromSeconds(60));

        itemA.OnHand.Should().Be(initialStock - confirmedCount);
        itemB.OnHand.Should().Be(initialStock - confirmedCount);
        itemA.Available.Should().BeGreaterThanOrEqualTo(0);
        itemB.Available.Should().BeGreaterThanOrEqualTo(0);
    }

    private static async Task<List<OrderData>> WaitUntilNoLongerPendingAsync(
        List<(HttpClient Client, Guid OrderId)> orders, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(60));
        List<OrderData> last;
        do
        {
            last = new List<OrderData>(orders.Count);
            foreach (var (client, orderId) in orders)
                last.Add(await GetOrderAsync(client, orderId));

            if (last.All(o => o.Status != "PendingStock")) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        var stillPending = last.Count(o => o.Status == "PendingStock");
        throw new TimeoutException($"{stillPending}/{orders.Count} orders were still PendingStock after {timeout}.");
    }
}

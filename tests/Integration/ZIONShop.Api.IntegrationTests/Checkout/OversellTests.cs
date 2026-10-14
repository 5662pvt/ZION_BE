using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Inventory.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using InventoryOrderCreatedHandler = ZIONShop.Inventory.Application.IntegrationEventHandlers.OrderCreatedIntegrationEventHandler;

namespace ZIONShop.Api.IntegrationTests.Checkout;

/// <summary>
/// Stresses the atomic conditional-UPDATE reservation path (<c>IInventoryItemRepository.TryReserveAsync</c>)
/// added to replace the old "load item, Reserve(), save with xmin, retry 3x in a fresh scope" handler --
/// under real contention that approach either oversold or produced false "Hết hàng" cancellations while
/// stock was still available. These tests assert the new behavior never oversells, never goes negative,
/// and never deadlocks when two orders touch the same two products in opposite order.
///
/// They drive <c>OrderCreatedIntegrationEventHandler</c> (Inventory's reservation handler) directly, each
/// invocation resolved in its own DI scope (own DbContext/connection) and released from a shared start gate
/// at the same time, so the reservation layer is actually exercised under concurrency. Going through the
/// HTTP API + outbox instead would not do this: the InMemory event bus dispatches one handler at a time per
/// publish, and the outbox publisher drains its table one message at a time, so an HTTP-level test never has
/// more than one reservation attempt in flight no matter how many requests are fired concurrently.
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
    public async Task FiftyConcurrentReservationHandlers_Should_ReserveExactlyTen_When_OnHandIsTen()
    {
        var admin = await AdminClientAsync();
        var (productId, sku) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var events = Enumerable.Range(0, 50)
            .Select(_ => new OrderCreatedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), 100000m,
                new[] { new OrderLine(productId, 1, 100000m, false) },
                "COD", "VND"))
            .ToList();

        await RunConcurrentlyAsync(events);

        var orderIds = events.Select(e => e.OrderId).ToHashSet();
        using var verifyScope = _factory.Services.CreateScope();
        var inventoryDb = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        var reservedQuantity = await inventoryDb.StockReservations
            .Where(r => orderIds.Contains(r.OrderId))
            .SumAsync(r => r.Quantity);
        reservedQuantity.Should().Be(10, "10 units of stock can satisfy exactly 10 of the 50 single-unit reservation attempts");

        var reservationCount = await inventoryDb.StockReservations.CountAsync(r => orderIds.Contains(r.OrderId));
        reservationCount.Should().Be(10);

        var failedCount = await CountMatchingFailuresAsync(inventoryDb, orderIds);
        failedCount.Should().Be(40);

        var item = await GetInventoryItemBySkuAsync(admin, sku);
        item.Reserved.Should().Be(10);
        item.Available.Should().Be(0);
    }

    [Fact]
    public async Task TwentyConcurrentReservationHandlers_Should_NotDeadlock_When_TwoProductsAlternateOrder()
    {
        var admin = await AdminClientAsync();
        var (productA, skuA) = await CreatePublishedProductAsync(admin);
        var (productB, skuB) = await CreatePublishedProductAsync(admin);
        const int initialStock = 1000;
        await AdjustStockAsync(admin, productA, initialStock);
        await AdjustStockAsync(admin, productB, initialStock);

        var events = new List<OrderCreatedIntegrationEvent>();
        for (var i = 0; i < 10; i++)
        {
            // Alternating line order per event -- the handler itself normalizes by ProductId before
            // reserving, so this exercises that normalization rather than relying on it.
            events.Add(new OrderCreatedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), 200000m,
                new[] { new OrderLine(productA, 1, 100000m, false), new OrderLine(productB, 1, 100000m, false) },
                "COD", "VND"));
            events.Add(new OrderCreatedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), 200000m,
                new[] { new OrderLine(productB, 1, 100000m, false), new OrderLine(productA, 1, 100000m, false) },
                "COD", "VND"));
        }

        await RunConcurrentlyAsync(events); // a deadlock would surface here as an exception

        var orderIds = events.Select(e => e.OrderId).ToHashSet();
        using var verifyScope = _factory.Services.CreateScope();
        var inventoryDb = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var reservedQuantity = await inventoryDb.StockReservations
            .Where(r => orderIds.Contains(r.OrderId))
            .SumAsync(r => r.Quantity);
        reservedQuantity.Should().Be(events.Count * 2, "stock is abundant, so every order reserves both of its lines");

        var itemA = await GetInventoryItemBySkuAsync(admin, skuA);
        var itemB = await GetInventoryItemBySkuAsync(admin, skuB);
        itemA.Reserved.Should().Be(events.Count);
        itemB.Reserved.Should().Be(events.Count);
        itemA.Available.Should().BeGreaterThanOrEqualTo(0);
        itemB.Available.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Resolves <c>IIntegrationEventHandler&lt;OrderCreatedIntegrationEvent&gt;</c> for each event in its own
    /// DI scope (own DbContext/connection), then releases every invocation from a shared start gate at once so
    /// they really overlap instead of running one after another.
    /// </summary>
    private async Task RunConcurrentlyAsync(IReadOnlyList<OrderCreatedIntegrationEvent> events)
    {
        var startGate = new SemaphoreSlim(0);
        var tasks = events.Select(async @event =>
        {
            using var scope = _factory.Services.CreateScope();
            var handler = scope.ServiceProvider.GetServices<IIntegrationEventHandler<OrderCreatedIntegrationEvent>>()
                .Single(h => h.GetType().GetGenericArguments().Contains(typeof(InventoryOrderCreatedHandler)));

            await startGate.WaitAsync();
            await handler.HandleAsync(@event, CancellationToken.None);
        }).ToList();

        await Task.Delay(50); // give every task a chance to reach the gate before releasing it
        startGate.Release(events.Count);

        await Task.WhenAll(tasks); // no exception is part of the assertion
    }

    private static async Task<int> CountMatchingFailuresAsync(InventoryDbContext db, IReadOnlySet<Guid> orderIds)
    {
        var failedMessages = await db.Set<OutboxMessage>()
            .Where(m => m.Type.Contains(nameof(InventoryReservationFailedIntegrationEvent)))
            .Select(m => m.Content)
            .ToListAsync();

        var failedOrderIds = failedMessages
            .Select(c => JsonSerializer.Deserialize<InventoryReservationFailedIntegrationEvent>(c)!.OrderId)
            .ToHashSet();

        return orderIds.Count(id => failedOrderIds.Contains(id));
    }
}

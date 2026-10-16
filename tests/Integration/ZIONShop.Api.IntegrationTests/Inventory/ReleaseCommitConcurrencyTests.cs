using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.Inventory.Application.Services;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using InventoryOrderCreatedHandler = ZIONShop.Inventory.Application.IntegrationEventHandlers.OrderCreatedIntegrationEventHandler;
using InventoryOrderCancelledHandler = ZIONShop.Inventory.Application.IntegrationEventHandlers.OrderCancelledIntegrationEventHandler;
using InventoryOrderConfirmedHandler = ZIONShop.Inventory.Application.IntegrationEventHandlers.OrderConfirmedIntegrationEventHandler;

namespace ZIONShop.Api.IntegrationTests.Inventory;

/// <summary>
/// W16 cleanup: release/commit used to load a tracked <c>InventoryItem</c> and save it with the xmin
/// concurrency token in EF's own (unordered) batching order, instead of the immediate, caller-ordered
/// atomic UPDATE that <c>TryReserveAsync</c> already used. Two concurrent multi-product operations that
/// touched the same products in opposite order (e.g. a cancel releasing A,B while another order reserves
/// B,A) could deadlock (Postgres 40P01) or throw <c>DbUpdateConcurrencyException</c>. These tests drive the
/// real handlers/processor concurrently against a real Postgres database (same approach as
/// <c>OversellTests</c>) and assert they never throw and always end in a numerically consistent state.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class ReleaseCommitConcurrencyTests
{
    private readonly ApiFactory _factory;

    public ReleaseCommitConcurrencyTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    [Fact]
    public async Task ConcurrentCancelsAndReserves_Should_NotDeadlock_When_TwoProductsAlternateOrder()
    {
        var admin = await AdminClientAsync();
        var (productA, skuA) = await CreatePublishedProductAsync(admin);
        var (productB, skuB) = await CreatePublishedProductAsync(admin);
        const int initialStock = 1000;
        await AdjustStockAsync(admin, productA, initialStock);
        await AdjustStockAsync(admin, productB, initialStock);

        const int pairCount = 10; // 10 pre-existing orders to cancel + 10 new orders to reserve = 20 concurrent calls
        var toCancelOrderIds = Enumerable.Range(0, pairCount).Select(_ => Guid.NewGuid()).ToList();
        await SeedActiveReservationPairAsync(toCancelOrderIds, productA, productB);

        var toReserveEvents = new List<OrderCreatedIntegrationEvent>();
        for (var i = 0; i < pairCount; i++)
        {
            // Alternating line order per event so the reservation side also exercises both directions.
            var lines = i % 2 == 0
                ? new[] { new OrderLine(productA, 1, 100000m, false), new OrderLine(productB, 1, 100000m, false) }
                : new[] { new OrderLine(productB, 1, 100000m, false), new OrderLine(productA, 1, 100000m, false) };
            toReserveEvents.Add(new OrderCreatedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), 200000m, lines, "COD", "VND"));
        }

        var operations = new List<Func<Task>>();
        operations.AddRange(toCancelOrderIds.Select(orderId => (Func<Task>)(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var handler = ResolveHandler<OrderCancelledIntegrationEvent, InventoryOrderCancelledHandler>(scope);
            await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, Guid.NewGuid(), "Integration test cancel"), CancellationToken.None);
        })));
        operations.AddRange(toReserveEvents.Select(@event => (Func<Task>)(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var handler = ResolveHandler<OrderCreatedIntegrationEvent, InventoryOrderCreatedHandler>(scope);
            await handler.HandleAsync(@event, CancellationToken.None);
        })));

        await RunConcurrentlyAsync(operations); // a deadlock/DbUpdateConcurrencyException would surface here

        var itemA = await GetInventoryItemBySkuAsync(admin, skuA);
        var itemB = await GetInventoryItemBySkuAsync(admin, skuB);
        itemA.OnHand.Should().Be(initialStock);
        itemB.OnHand.Should().Be(initialStock);
        itemA.Reserved.Should().Be(pairCount, "the cancelled orders' reservations released while the new orders' reservations took their place");
        itemB.Reserved.Should().Be(pairCount);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var cancelledOrderIdSet = toCancelOrderIds.ToHashSet();
        (await db.StockReservations.Where(r => cancelledOrderIdSet.Contains(r.OrderId)).ToListAsync())
            .Should().OnlyContain(r => r.Status == ReservationStatus.Released)
            .And.HaveCount(pairCount * 2);

        var newOrderIdSet = toReserveEvents.Select(e => e.OrderId).ToHashSet();
        (await db.StockReservations.Where(r => newOrderIdSet.Contains(r.OrderId)).ToListAsync())
            .Should().OnlyContain(r => r.Status == ReservationStatus.Active)
            .And.HaveCount(pairCount * 2);
    }

    [Fact]
    public async Task ConcurrentExpiryAndCommits_Should_NotDeadlock_When_TwoProductsAlternateOrder()
    {
        var admin = await AdminClientAsync();
        var (productA, skuA) = await CreatePublishedProductAsync(admin);
        var (productB, skuB) = await CreatePublishedProductAsync(admin);
        const int initialStock = 1000;
        await AdjustStockAsync(admin, productA, initialStock);
        await AdjustStockAsync(admin, productB, initialStock);

        const int pairCount = 10;
        var toExpireOrderIds = Enumerable.Range(0, pairCount).Select(_ => Guid.NewGuid()).ToList();
        await SeedActiveReservationPairAsync(toExpireOrderIds, productA, productB, expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var toCommitOrderIds = Enumerable.Range(0, pairCount).Select(_ => Guid.NewGuid()).ToList();
        await SeedActiveReservationPairAsync(toCommitOrderIds, productA, productB);

        var operations = new List<Func<Task>>
        {
            async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<ReservationExpiryProcessor>();
                await processor.ExpireDueReservationsAsync(CancellationToken.None);
            }
        };
        operations.AddRange(toCommitOrderIds.Select(orderId => (Func<Task>)(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var handler = ResolveHandler<OrderConfirmedIntegrationEvent, InventoryOrderConfirmedHandler>(scope);
            await handler.HandleAsync(new OrderConfirmedIntegrationEvent(orderId, Guid.NewGuid()), CancellationToken.None);
        })));

        await RunConcurrentlyAsync(operations); // a deadlock/DbUpdateConcurrencyException would surface here

        var itemA = await GetInventoryItemBySkuAsync(admin, skuA);
        var itemB = await GetInventoryItemBySkuAsync(admin, skuB);
        itemA.Reserved.Should().Be(0, "every seeded reservation was either expired-released or committed");
        itemB.Reserved.Should().Be(0);
        itemA.OnHand.Should().Be(initialStock - pairCount, "the committed orders' quantity left OnHand; the expired orders' did not");
        itemB.OnHand.Should().Be(initialStock - pairCount);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var expiredOrderIdSet = toExpireOrderIds.ToHashSet();
        (await db.StockReservations.Where(r => expiredOrderIdSet.Contains(r.OrderId)).ToListAsync())
            .Should().OnlyContain(r => r.Status == ReservationStatus.Expired)
            .And.HaveCount(pairCount * 2);

        var committedOrderIdSet = toCommitOrderIds.ToHashSet();
        (await db.StockReservations.Where(r => committedOrderIdSet.Contains(r.OrderId)).ToListAsync())
            .Should().OnlyContain(r => r.Status == ReservationStatus.Committed)
            .And.HaveCount(pairCount * 2);
    }

    /// <summary>Seeds one Active reservation for productA and one for productB per order, bumping each item's Reserved to match.</summary>
    private async Task SeedActiveReservationPairAsync(IReadOnlyList<Guid> orderIds, Guid productA, Guid productB, DateTime? expiresAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        var now = DateTime.UtcNow;

        var itemA = await db.InventoryItems.SingleAsync(i => i.ProductId == productA);
        var itemB = await db.InventoryItems.SingleAsync(i => i.ProductId == productB);

        foreach (var orderId in orderIds)
        {
            itemA.Reserve(1);
            itemB.Reserve(1);
            await db.StockReservations.AddAsync(StockReservation.Create(orderId, productA, 1, expiresAt, now));
            await db.StockReservations.AddAsync(StockReservation.Create(orderId, productB, 1, expiresAt, now));
        }

        await db.SaveChangesAsync();
    }

    private static IIntegrationEventHandler<TEvent> ResolveHandler<TEvent, TInner>(IServiceScope scope)
        where TEvent : IntegrationEvent =>
        scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>()
            .Single(h => h.GetType().GetGenericArguments().Contains(typeof(TInner)));

    /// <summary>
    /// Releases every operation from a shared start gate at once so they really overlap instead of running
    /// one after another -- same approach as <c>OversellTests.RunConcurrentlyAsync</c>.
    /// </summary>
    private static async Task RunConcurrentlyAsync(IReadOnlyList<Func<Task>> operations)
    {
        var startGate = new SemaphoreSlim(0);
        var tasks = operations.Select(async operation =>
        {
            await startGate.WaitAsync();
            await operation();
        }).ToList();

        await Task.Delay(50); // give every task a chance to reach the gate before releasing it
        startGate.Release(operations.Count);

        await Task.WhenAll(tasks); // no exception is part of the assertion
    }
}

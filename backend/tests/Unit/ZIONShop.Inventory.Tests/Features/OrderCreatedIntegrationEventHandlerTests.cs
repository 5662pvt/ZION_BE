using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Tests.TestSupport;

namespace ZIONShop.Inventory.Tests.Features;

public class OrderCreatedIntegrationEventHandlerTests
{
    private static OrderCreatedIntegrationEventHandler CreateHandler(InventoryScopeFactory scopeFactory, FakeTimeProvider clock) =>
        new(scopeFactory.CreateScopeFactory(), clock, NullLogger<OrderCreatedIntegrationEventHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Should_ReserveAllLines_When_AllAvailable()
    {
        var scopeFactory = new InventoryScopeFactory();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        using (var seedDb = scopeFactory.CreateDbContext())
        {
            await seedDb.InventoryItems.AddRangeAsync(
                InventoryItem.Create(productA, "SKU-A", "Product A", 10),
                InventoryItem.Create(productB, "SKU-B", "Product B", 10));
            await seedDb.SaveChangesAsync();
        }

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = CreateHandler(scopeFactory, clock);
        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(productB, 3, 20m) },
            "COD", "VND");

        await handler.HandleAsync(@event, CancellationToken.None);

        using var verifyDb = scopeFactory.CreateDbContext();
        var itemA = verifyDb.InventoryItems.Single(i => i.ProductId == productA);
        var itemB = verifyDb.InventoryItems.Single(i => i.ProductId == productB);
        itemA.Reserved.Should().Be(2);
        itemB.Reserved.Should().Be(3);

        var reservations = verifyDb.StockReservations.Where(r => r.OrderId == orderId).ToList();
        reservations.Should().HaveCount(2);

        scopeFactory.OutboxEvents.OfType<InventoryReservedIntegrationEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_Should_ReserveNothing_When_AnyLineUnavailable()
    {
        var scopeFactory = new InventoryScopeFactory();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        using (var seedDb = scopeFactory.CreateDbContext())
        {
            await seedDb.InventoryItems.AddRangeAsync(
                InventoryItem.Create(productA, "SKU-A", "Product A", 10),
                InventoryItem.Create(productB, "SKU-B", "Product B", 1)); // not enough for qty 3
            await seedDb.SaveChangesAsync();
        }

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = CreateHandler(scopeFactory, clock);
        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(productB, 3, 20m) },
            "COD", "VND");

        await handler.HandleAsync(@event, CancellationToken.None);

        using var verifyDb = scopeFactory.CreateDbContext();
        verifyDb.InventoryItems.Single(i => i.ProductId == productA).Reserved.Should().Be(0);
        verifyDb.InventoryItems.Single(i => i.ProductId == productB).Reserved.Should().Be(0);
        verifyDb.StockReservations.Any(r => r.OrderId == orderId).Should().BeFalse();

        var failed = scopeFactory.OutboxEvents.OfType<InventoryReservationFailedIntegrationEvent>().Single();
        failed.UnavailableProductIds.Should().Contain(productB);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwiceForSameOrder()
    {
        var scopeFactory = new InventoryScopeFactory();
        var productA = Guid.NewGuid();

        using (var seedDb = scopeFactory.CreateDbContext())
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productA, "SKU-A", "Product A", 10));
            await seedDb.SaveChangesAsync();
        }

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = CreateHandler(scopeFactory, clock);
        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m) },
            "COD", "VND");

        await handler.HandleAsync(@event, CancellationToken.None);
        await handler.HandleAsync(@event, CancellationToken.None);

        using var verifyDb = scopeFactory.CreateDbContext();
        verifyDb.InventoryItems.Single(i => i.ProductId == productA).Reserved.Should().Be(2);
        verifyDb.StockReservations.Count(r => r.OrderId == orderId).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_SetNoExpiry_When_PaymentMethodIsCod()
    {
        var scopeFactory = new InventoryScopeFactory();
        var productA = Guid.NewGuid();

        using (var seedDb = scopeFactory.CreateDbContext())
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productA, "SKU-A", "Product A", 10));
            await seedDb.SaveChangesAsync();
        }

        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = CreateHandler(scopeFactory, clock);
        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), 10m, new[] { new OrderLine(productA, 1, 10m) }, "COD", "VND");

        await handler.HandleAsync(@event, CancellationToken.None);

        using var verifyDb = scopeFactory.CreateDbContext();
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_SetFifteenMinuteExpiry_When_PaymentMethodIsOnline()
    {
        var scopeFactory = new InventoryScopeFactory();
        var productA = Guid.NewGuid();

        using (var seedDb = scopeFactory.CreateDbContext())
        {
            await seedDb.InventoryItems.AddAsync(InventoryItem.Create(productA, "SKU-A", "Product A", 10));
            await seedDb.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var clock = new FakeTimeProvider(now);
        var handler = CreateHandler(scopeFactory, clock);
        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), 10m, new[] { new OrderLine(productA, 1, 10m) }, "Online", "VND");

        await handler.HandleAsync(@event, CancellationToken.None);

        using var verifyDb = scopeFactory.CreateDbContext();
        verifyDb.StockReservations.Single(r => r.OrderId == orderId).ExpiresAt.Should().Be(now.UtcDateTime.AddMinutes(15));
    }
}

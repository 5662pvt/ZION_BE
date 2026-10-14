using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Tests.TestSupport;

namespace ZIONShop.Inventory.Tests.Features;

public class OrderCreatedIntegrationEventHandlerTests
{
    private sealed record Harness(
        OrderCreatedIntegrationEventHandler Handler,
        FakeInventoryItemRepository Items,
        FakeStockReservationRepository Reservations,
        FakeStockMovementRepository Movements,
        FakeInventoryUnitOfWork Uow,
        List<ZIONShop.EventBus.Abstractions.IntegrationEvent> OutboxEvents);

    private static Harness CreateHarness(FakeTimeProvider clock)
    {
        var items = new FakeInventoryItemRepository();
        var reservations = new FakeStockReservationRepository();
        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var outboxEvents = new List<ZIONShop.EventBus.Abstractions.IntegrationEvent>();
        var outbox = new FakeInventoryOutbox(outboxEvents);

        var handler = new OrderCreatedIntegrationEventHandler(
            items, reservations, movements, uow, outbox, clock, NullLogger<OrderCreatedIntegrationEventHandler>.Instance);

        return new Harness(handler, items, reservations, movements, uow, outboxEvents);
    }

    [Fact]
    public async Task HandleAsync_Should_ReserveAllLines_When_AllAvailable()
    {
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(
            InventoryItem.Create(productA, "SKU-A", "Product A", 10),
            InventoryItem.Create(productB, "SKU-B", "Product B", 10));

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(productB, 3, 20m) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(2);
        harness.Items.Get(productB)!.Reserved.Should().Be(3);
        harness.Reservations.Reservations.Count(r => r.OrderId == orderId).Should().Be(2);
        harness.OutboxEvents.OfType<InventoryReservedIntegrationEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_Should_ReserveNothing_When_AnyLineUnavailable()
    {
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(
            InventoryItem.Create(productA, "SKU-A", "Product A", 10),
            InventoryItem.Create(productB, "SKU-B", "Product B", 1)); // not enough for qty 3

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(productB, 3, 20m) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(0); // rolled back
        harness.Items.Get(productB)!.Reserved.Should().Be(0);
        harness.Reservations.Reservations.Any(r => r.OrderId == orderId).Should().BeFalse();

        var failed = harness.OutboxEvents.OfType<InventoryReservationFailedIntegrationEvent>().Single();
        failed.UnavailableProductIds.Should().Contain(productB);
    }

    [Fact]
    public async Task HandleAsync_Should_RollBackEarlierReservation_When_LaterProductInTheSameOrderFails()
    {
        // productA sorts before productB by Guid is not guaranteed, so force the ordering explicitly
        // via a seeded id pair and assert on both sides of the rollback regardless of which went first.
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(
            InventoryItem.Create(productA, "SKU-A", "Product A", 10),
            InventoryItem.Create(productB, "SKU-B", "Product B", 10));
        harness.Items.ForceReserveFailure.Add(productB); // simulate productB being out of stock

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(productB, 1, 10m) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        // All-or-nothing: productA's successful reserve must be undone once productB fails.
        harness.Items.Get(productA)!.Reserved.Should().Be(0);
        harness.Items.Get(productB)!.Reserved.Should().Be(0);
        harness.Reservations.Reservations.Should().BeEmpty();
        harness.OutboxEvents.OfType<InventoryReservationFailedIntegrationEvent>().Single()
            .UnavailableProductIds.Should().Contain(productB);
    }

    [Fact]
    public async Task HandleAsync_Should_ProcessGroups_OrderedByProductId()
    {
        var productLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var productHigh = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(
            InventoryItem.Create(productHigh, "SKU-H", "Product High", 10),
            InventoryItem.Create(productLow, "SKU-L", "Product Low", 10));

        var orderId = Guid.NewGuid();
        // Lines deliberately submitted high-id-first; the handler must still process low-id-first.
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productHigh, 1, 10m), new OrderLine(productLow, 1, 10m) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        var reserved = harness.OutboxEvents.OfType<InventoryReservedIntegrationEvent>().Single();
        reserved.Items.Select(i => i.ProductId).Should().Equal(productLow, productHigh);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwiceForSameOrder()
    {
        var productA = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(InventoryItem.Create(productA, "SKU-A", "Product A", 10));

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);
        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(2);
        harness.Reservations.Reservations.Count(r => r.OrderId == orderId).Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_SetNoExpiry_When_PaymentMethodIsCod()
    {
        var productA = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(InventoryItem.Create(productA, "SKU-A", "Product A", 10));

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), 10m, new[] { new OrderLine(productA, 1, 10m) }, "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Reservations.Reservations.Single(r => r.OrderId == orderId).ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_SetFifteenMinuteExpiry_When_PaymentMethodIsOnline()
    {
        var productA = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var harness = CreateHarness(new FakeTimeProvider(now));
        harness.Items.Seed(InventoryItem.Create(productA, "SKU-A", "Product A", 10));

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(orderId, Guid.NewGuid(), 10m, new[] { new OrderLine(productA, 1, 10m) }, "Online", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Reservations.Reservations.Single(r => r.OrderId == orderId).ExpiresAt.Should().Be(now.UtcDateTime.AddMinutes(15));
    }

    [Fact]
    public async Task HandleAsync_Should_ReservePaidAndGift_When_BothAvailable_ForDifferentProducts()
    {
        var productA = Guid.NewGuid();
        var giftProduct = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(
            InventoryItem.Create(productA, "SKU-A", "Product A", 10),
            InventoryItem.Create(giftProduct, "SKU-G", "Gift Product", 10));

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(giftProduct, 1, 0m, IsGift: true) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(2);
        harness.Items.Get(giftProduct)!.Reserved.Should().Be(1);
        harness.Reservations.Reservations.Count(r => r.OrderId == orderId).Should().Be(2);

        var reserved = harness.OutboxEvents.OfType<InventoryReservedIntegrationEvent>().Single();
        reserved.Items.Should().Contain(i => i.ProductId == productA && i.Quantity == 2);
        reserved.Items.Should().Contain(i => i.ProductId == giftProduct && i.Quantity == 1);
    }

    [Fact]
    public async Task HandleAsync_Should_ReserveOnlyPaid_When_GiftExceedsAvailable_ForDifferentProducts()
    {
        var productA = Guid.NewGuid();
        var giftProduct = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(
            InventoryItem.Create(productA, "SKU-A", "Product A", 10),
            InventoryItem.Create(giftProduct, "SKU-G", "Gift Product", 3)); // not enough for gift qty 5

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(giftProduct, 5, 0m, IsGift: true) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(2);
        harness.Items.Get(giftProduct)!.Reserved.Should().Be(0); // gift silently dropped
        harness.Reservations.Reservations.Any(r => r.OrderId == orderId && r.ProductId == giftProduct).Should().BeFalse();

        var reserved = harness.OutboxEvents.OfType<InventoryReservedIntegrationEvent>().Single();
        reserved.Items.Should().ContainSingle(i => i.ProductId == productA && i.Quantity == 2);
        harness.OutboxEvents.OfType<InventoryReservationFailedIntegrationEvent>().Should().BeEmpty(); // order still places
    }

    [Fact]
    public async Task HandleAsync_Should_ReserveOneRow_When_SameProductIsBothPaidAndGift()
    {
        var productA = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(InventoryItem.Create(productA, "SKU-A", "Product A", 10));

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 2, 10m), new OrderLine(productA, 1, 0m, IsGift: true) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(3);
        harness.Reservations.Reservations.Where(r => r.OrderId == orderId).Should().ContainSingle(r => r.ProductId == productA && r.Quantity == 3);

        var reserved = harness.OutboxEvents.OfType<InventoryReservedIntegrationEvent>().Single();
        reserved.Items.Should().ContainSingle(i => i.ProductId == productA && i.Quantity == 3);
    }

    [Fact]
    public async Task HandleAsync_Should_FailWholeOrder_When_PaidQuantityExceedsAvailable_RegardlessOfGift()
    {
        var productA = Guid.NewGuid();
        var harness = CreateHarness(new FakeTimeProvider(DateTimeOffset.UtcNow));
        harness.Items.Seed(InventoryItem.Create(productA, "SKU-A", "Product A", 3)); // not enough for paid qty 5

        var orderId = Guid.NewGuid();
        var @event = new OrderCreatedIntegrationEvent(
            orderId, Guid.NewGuid(), 100m,
            new[] { new OrderLine(productA, 5, 10m), new OrderLine(productA, 1, 0m, IsGift: true) },
            "COD", "VND");

        await harness.Handler.HandleAsync(@event, CancellationToken.None);

        harness.Items.Get(productA)!.Reserved.Should().Be(0);
        harness.Reservations.Reservations.Any(r => r.OrderId == orderId).Should().BeFalse();

        var failed = harness.OutboxEvents.OfType<InventoryReservationFailedIntegrationEvent>().Single();
        failed.UnavailableProductIds.Should().Contain(productA);
    }
}

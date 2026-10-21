using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Orders;
using ZIONShop.Inventory.Application.IntegrationEventHandlers;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Tests.TestSupport;

namespace ZIONShop.Inventory.Tests.Features;

public class OrderReturnReceivedIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_ReturnCommittedReservations_OrderedByProductId_And_MarkThemReturned()
    {
        var productLow = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var productHigh = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(
            InventoryItem.Create(productHigh, productHigh, "SKU-H", "Product High", null, 10),
            InventoryItem.Create(productLow, productLow, "SKU-L", "Product Low", null, 10));
        items.Get(productHigh)!.Reserve(2);
        items.Get(productHigh)!.Commit(2);
        items.Get(productLow)!.Reserve(5);
        items.Get(productLow)!.Commit(5);

        var recorder = new RecordingInventoryItemRepository(items);
        var reservations = new FakeStockReservationRepository();
        // Submitted high-id-first -- the handler must still issue its Try*Async calls low-id-first.
        var highReservation = StockReservation.Create(orderId, productHigh, productHigh, 2, null, DateTime.UtcNow);
        highReservation.MarkCommitted();
        await reservations.AddAsync(highReservation);
        var lowReservation = StockReservation.Create(orderId, productLow, productLow, 5, null, DateTime.UtcNow);
        lowReservation.MarkCommitted();
        await reservations.AddAsync(lowReservation);

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var handler = new OrderReturnReceivedIntegrationEventHandler(
            reservations, recorder, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<OrderReturnReceivedIntegrationEventHandler>.Instance);

        var @event = new OrderReturnReceivedIntegrationEvent(
            orderId, Guid.NewGuid(), Guid.NewGuid(), 100000m, "COD",
            new[] { new ReturnLine(productHigh, 2), new ReturnLine(productLow, 5) });

        await handler.HandleAsync(@event, CancellationToken.None);

        recorder.Calls.Select(c => c.VariantId).Should().Equal(productLow, productHigh);
        recorder.Calls.Select(c => c.Operation).Should().Equal("Return", "Return");

        items.Get(productLow)!.OnHand.Should().Be(10);
        items.Get(productHigh)!.OnHand.Should().Be(10);

        reservations.Reservations.Should().OnlyContain(r => r.Status == ReservationStatus.Returned);
        movements.Movements.Should().HaveCount(2);
        movements.Movements.Should().OnlyContain(m => m.Type == StockMovementType.Return && m.Reference == orderId.ToString());
        uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_BeNoOp_When_CalledAgainAfterReservationsAlreadyReturned()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 10));
        items.Get(productId)!.Reserve(4);
        items.Get(productId)!.Commit(4);

        var reservations = new FakeStockReservationRepository();
        var reservation = StockReservation.Create(orderId, productId, productId, 4, null, DateTime.UtcNow);
        reservation.MarkCommitted();
        await reservations.AddAsync(reservation);

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var @event = new OrderReturnReceivedIntegrationEvent(
            orderId, Guid.NewGuid(), Guid.NewGuid(), 50000m, "COD", new[] { new ReturnLine(productId, 4) });

        var handler1 = new OrderReturnReceivedIntegrationEventHandler(
            reservations, items, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<OrderReturnReceivedIntegrationEventHandler>.Instance);
        await handler1.HandleAsync(@event, CancellationToken.None);

        items.Get(productId)!.OnHand.Should().Be(10);
        movements.Movements.Should().HaveCount(1);

        // Redelivery (e.g. inbox retry without dedupe): reservation is now Returned, not Committed --
        // nothing left to process, so OnHand and movements must not change again.
        var handler2 = new OrderReturnReceivedIntegrationEventHandler(
            reservations, items, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<OrderReturnReceivedIntegrationEventHandler>.Instance);
        await handler2.HandleAsync(@event, CancellationToken.None);

        items.Get(productId)!.OnHand.Should().Be(10);
        movements.Movements.Should().HaveCount(1);
        uow.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_Should_IgnoreNonCommittedReservations()
    {
        var productId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 10));

        var reservations = new FakeStockReservationRepository();
        var activeReservation = StockReservation.Create(orderId, productId, productId, 4, null, DateTime.UtcNow);
        await reservations.AddAsync(activeReservation); // still Active, never confirmed/committed

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var handler = new OrderReturnReceivedIntegrationEventHandler(
            reservations, items, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<OrderReturnReceivedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(
            new OrderReturnReceivedIntegrationEvent(orderId, Guid.NewGuid(), Guid.NewGuid(), 50000m, "COD", new[] { new ReturnLine(productId, 4) }),
            CancellationToken.None);

        movements.Movements.Should().BeEmpty();
        reservations.Reservations.Single().Status.Should().Be(ReservationStatus.Active);
        uow.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_When_EventLinesDisagreeWithReservations()
    {
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var items = new FakeInventoryItemRepository();
        items.Seed(InventoryItem.Create(productId, productId, "SKU-A", "Product A", null, 10));
        items.Get(productId)!.Reserve(4);
        items.Get(productId)!.Commit(4);

        var reservations = new FakeStockReservationRepository();
        var reservation = StockReservation.Create(orderId, productId, productId, 4, null, DateTime.UtcNow);
        reservation.MarkCommitted();
        await reservations.AddAsync(reservation);

        var movements = new FakeStockMovementRepository();
        var uow = new FakeInventoryUnitOfWork(items);
        var handler = new OrderReturnReceivedIntegrationEventHandler(
            reservations, items, movements, uow, new FakeTimeProvider(DateTimeOffset.UtcNow),
            NullLogger<OrderReturnReceivedIntegrationEventHandler>.Instance);

        // Lines disagree with the committed reservation (different product entirely) -- must log a
        // warning, not fail, and still restock from the reservations.
        var act = async () => await handler.HandleAsync(
            new OrderReturnReceivedIntegrationEvent(orderId, Guid.NewGuid(), Guid.NewGuid(), 50000m, "COD", new[] { new ReturnLine(otherProductId, 4) }),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
        items.Get(productId)!.OnHand.Should().Be(10);
        reservations.Reservations.Single().Status.Should().Be(ReservationStatus.Returned);
    }
}

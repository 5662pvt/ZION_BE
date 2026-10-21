using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Application.IntegrationEvents;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;
using ZIONShop.Orders.Tests.Fakes;

namespace ZIONShop.Orders.Tests.IntegrationEvents;

public class InventoryReservedIntegrationEventHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db));
    }

    private static async Task<Order> SeedOrderAsync(OrdersDbContext db, OrderRepository orders, PaymentMethod paymentMethod)
    {
        var order = Order.Place(
            Guid.NewGuid(), paymentMethod, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task HandleAsync_Should_SetConfirmed_When_PaymentMethodIsCod()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders, PaymentMethod.COD);

        var handler = new InventoryReservedIntegrationEventHandler(orders, db, new FakeGiftPromotionEvaluator());
        await handler.HandleAsync(new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15)));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task HandleAsync_Should_SetAwaitingPayment_When_PaymentMethodIsOnline()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders, PaymentMethod.Online);

        var handler = new InventoryReservedIntegrationEventHandler(orders, db, new FakeGiftPromotionEvaluator());
        await handler.HandleAsync(new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15)));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.AwaitingPayment);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders, PaymentMethod.COD);

        var handler = new InventoryReservedIntegrationEventHandler(orders, db, new FakeGiftPromotionEvaluator());
        var @event = new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15));

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event); // redelivery must not throw or change state

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task HandleAsync_Should_RequestReservationRelease_When_OrderAlreadyCancelled()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders, PaymentMethod.COD);
        order.Cancel("Khách hủy đơn", isStaff: false);
        orders.Update(order);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new InventoryReservedIntegrationEventHandler(orders, db, new FakeGiftPromotionEvaluator());
        await handler.HandleAsync(new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15)));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Cancelled); // the late reservation never flips an already-cancelled order

        var outboxMessages = await db.Set<OutboxMessage>().ToListAsync();
        outboxMessages.Should().ContainSingle(m =>
            m.Type.Contains(nameof(OrderReservationReleaseRequestedIntegrationEvent)) && m.Content.Contains(order.Id.ToString()));
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_OrderUnknown()
    {
        var (db, orders) = BuildScope();
        var handler = new InventoryReservedIntegrationEventHandler(orders, db, new FakeGiftPromotionEvaluator());

        await handler.HandleAsync(new InventoryReservedIntegrationEvent(Guid.NewGuid(), Array.Empty<ReservedItem>(), DateTime.UtcNow));
        // no exception thrown is the assertion
    }

    [Fact]
    public async Task HandleAsync_Should_DropShortGiftLine_But_NotReleasePromotion_When_AnotherGiftLineOfItStillReserved()
    {
        var (db, orders) = BuildScope();
        var productId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        var giftProduct1 = Guid.NewGuid();
        var giftProduct2 = Guid.NewGuid();

        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(productId, "Widget", 100000m, 1) },
            null);
        order.AddGiftLine(giftProduct1, "Gift 1", 1, promotionId);
        order.AddGiftLine(giftProduct2, "Gift 2", 1, promotionId);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var giftEvaluator = new FakeGiftPromotionEvaluator();
        var handler = new InventoryReservedIntegrationEventHandler(orders, db, giftEvaluator);
        // giftProduct1 missing from the reserved items -> short -> dropped; giftProduct2 kept.
        var @event = new InventoryReservedIntegrationEvent(
            order.Id, new[] { new ReservedItem(productId, 1), new ReservedItem(giftProduct2, 1) }, DateTime.UtcNow.AddMinutes(15));

        await handler.HandleAsync(@event);

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Lines.Should().NotContain(l => l.ProductId == giftProduct1);
        reloaded.Lines.Should().Contain(l => l.ProductId == giftProduct2);
        reloaded.StatusHistory.Should().Contain(h => h.Note == "Quà tặng hết hàng – đã bỏ quà");
        giftEvaluator.ReleasedGiftCalls.Should().BeEmpty(); // promotion still has a kept gift line
    }

    [Fact]
    public async Task HandleAsync_Should_ReleaseGiftPromotion_When_AllOfItsGiftLinesAreDropped()
    {
        var (db, orders) = BuildScope();
        var productId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();
        var giftProductId = Guid.NewGuid();

        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(productId, "Widget", 100000m, 1) },
            null);
        order.AddGiftLine(giftProductId, "Gift", 1, promotionId);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var giftEvaluator = new FakeGiftPromotionEvaluator();
        var handler = new InventoryReservedIntegrationEventHandler(orders, db, giftEvaluator);
        // giftProductId missing entirely from the reserved items -> dropped, and it was the promotion's only line.
        var @event = new InventoryReservedIntegrationEvent(order.Id, new[] { new ReservedItem(productId, 1) }, DateTime.UtcNow.AddMinutes(15));

        await handler.HandleAsync(@event);

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Lines.Should().NotContain(l => l.IsGift);
        giftEvaluator.ReleasedGiftCalls.Should().ContainSingle(c => c.OrderId == order.Id && c.PromotionId == promotionId);
    }

    [Fact]
    public async Task HandleAsync_Should_NotDropGift_When_OnlyASiblingVariantOfTheSameProductIsShort()
    {
        var (db, orders) = BuildScope();
        var productId = Guid.NewGuid();
        var purchasedVariantId = Guid.NewGuid();
        var giftVariantId = Guid.NewGuid();
        var promotionId = Guid.NewGuid();

        // Purchased and gift lines are DIFFERENT variants of the SAME product -- dropping must key off VariantId, not ProductId.
        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(productId, "Áo thun", 100000m, 1, purchasedVariantId, "Đỏ") },
            null);
        order.AddGiftLine(productId, "Áo thun", 1, promotionId, giftVariantId, "Xanh");
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var giftEvaluator = new FakeGiftPromotionEvaluator();
        var handler = new InventoryReservedIntegrationEventHandler(orders, db, giftEvaluator);
        // Only the gift variant was actually reserved; the purchased variant came up short, but that must not touch the gift line.
        var @event = new InventoryReservedIntegrationEvent(
            order.Id, new[] { new ReservedItem(productId, 1, giftVariantId) }, DateTime.UtcNow.AddMinutes(15));

        await handler.HandleAsync(@event);

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Lines.Should().Contain(l => l.IsGift && l.VariantId == giftVariantId);
        giftEvaluator.ReleasedGiftCalls.Should().BeEmpty();
    }
}

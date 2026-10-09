using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Orders.Application.IntegrationEvents;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

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
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
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

        var handler = new InventoryReservedIntegrationEventHandler(orders, db);
        await handler.HandleAsync(new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15)));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task HandleAsync_Should_SetAwaitingPayment_When_PaymentMethodIsOnline()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders, PaymentMethod.Online);

        var handler = new InventoryReservedIntegrationEventHandler(orders, db);
        await handler.HandleAsync(new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15)));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.AwaitingPayment);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders, PaymentMethod.COD);

        var handler = new InventoryReservedIntegrationEventHandler(orders, db);
        var @event = new InventoryReservedIntegrationEvent(order.Id, Array.Empty<ReservedItem>(), DateTime.UtcNow.AddMinutes(15));

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event); // redelivery must not throw or change state

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public async Task HandleAsync_Should_NoOp_When_OrderUnknown()
    {
        var (db, orders) = BuildScope();
        var handler = new InventoryReservedIntegrationEventHandler(orders, db);

        await handler.HandleAsync(new InventoryReservedIntegrationEvent(Guid.NewGuid(), Array.Empty<ReservedItem>(), DateTime.UtcNow));
        // no exception thrown is the assertion
    }
}

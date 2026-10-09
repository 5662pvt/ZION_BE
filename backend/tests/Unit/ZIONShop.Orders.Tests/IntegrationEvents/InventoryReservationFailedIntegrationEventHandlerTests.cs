using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Orders.Application.IntegrationEvents;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.IntegrationEvents;

public class InventoryReservationFailedIntegrationEventHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db));
    }

    private static async Task<Order> SeedOrderAsync(OrdersDbContext db, OrderRepository orders)
    {
        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task HandleAsync_Should_CancelOrder_When_PendingStock()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders);

        var handler = new InventoryReservationFailedIntegrationEventHandler(orders, db);
        await handler.HandleAsync(new InventoryReservationFailedIntegrationEvent(order.Id, "Hết hàng", new[] { order.Lines.First().ProductId }));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Cancelled);
        reloaded.CancelReason.Should().Be("Hết hàng");
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, orders) = BuildScope();
        var order = await SeedOrderAsync(db, orders);
        var handler = new InventoryReservationFailedIntegrationEventHandler(orders, db);
        var @event = new InventoryReservationFailedIntegrationEvent(order.Id, "Hết hàng", Array.Empty<Guid>());

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Cancelled);
    }
}

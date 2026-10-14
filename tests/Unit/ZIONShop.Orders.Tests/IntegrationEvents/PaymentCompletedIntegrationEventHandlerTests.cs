using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Payments;
using ZIONShop.Orders.Application.IntegrationEvents;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.IntegrationEvents;

public class PaymentCompletedIntegrationEventHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db));
    }

    private static async Task<Order> SeedAwaitingPaymentOrderAsync(OrdersDbContext db, OrderRepository orders)
    {
        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.Online, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        order.ConfirmReservation(); // -> AwaitingPayment
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task HandleAsync_Should_SetPaid_When_AwaitingPayment()
    {
        var (db, orders) = BuildScope();
        var order = await SeedAwaitingPaymentOrderAsync(db, orders);

        var handler = new PaymentCompletedIntegrationEventHandler(orders, db);
        await handler.HandleAsync(new PaymentCompletedIntegrationEvent(order.Id, Guid.NewGuid(), order.Total, "FakeGateway"));

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwice()
    {
        var (db, orders) = BuildScope();
        var order = await SeedAwaitingPaymentOrderAsync(db, orders);
        var handler = new PaymentCompletedIntegrationEventHandler(orders, db);
        var @event = new PaymentCompletedIntegrationEvent(order.Id, Guid.NewGuid(), order.Total, "FakeGateway");

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        var reloaded = await orders.GetByIdAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Paid);
    }
}

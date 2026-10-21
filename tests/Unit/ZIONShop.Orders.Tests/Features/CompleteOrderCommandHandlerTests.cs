using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Application.Features.CompleteOrder;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class CompleteOrderCommandHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db));
    }

    private static async Task<Order> SeedShippedOrderAsync(OrdersDbContext db, OrderRepository orders)
    {
        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        order.ConfirmReservation();
        order.Ship();
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return order;
    }

    [Fact]
    public async Task Handle_Should_CompleteAndQueueOutboxEvent_When_OrderIsShipped()
    {
        var (db, orders) = BuildScope();
        var order = await SeedShippedOrderAsync(db, orders);

        var handler = new CompleteOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Completed");

        var outboxMessage = await db.Set<OutboxMessage>().SingleAsync();
        outboxMessage.Type.Should().Contain("OrderCompletedIntegrationEvent");
        outboxMessage.Content.Should().Contain(order.Id.ToString());
        outboxMessage.Content.Should().Contain(order.UserId.ToString());
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_OrderNotFound()
    {
        var (db, orders) = BuildScope();

        var handler = new CompleteOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CompleteOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.OrderNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_And_NotQueueOutboxEvent_When_OrderNotYetShipped()
    {
        var (db, orders) = BuildScope();
        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CompleteOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CompleteOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidTransition");
        (await db.Set<OutboxMessage>().AnyAsync()).Should().BeFalse();
    }
}

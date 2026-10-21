using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.IntegrationEvents;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.IntegrationEvents;

public class OrderRefundedIntegrationEventHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db), new ReturnRequestRepository(db));
    }

    private static async Task<(Order order, ReturnRequest request)> SeedReceivedRequestAsync(OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns)
    {
        var userId = Guid.NewGuid();
        var order = Order.Place(
            userId, PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        order.ConfirmReservation();
        order.Ship();
        order.Complete();
        await orders.AddAsync(order, CancellationToken.None);

        var request = ReturnRequest.Create(order.Id, userId, ReturnReason.Defective, null, order.Total, DateTime.UtcNow).Value;
        request.Approve(DateTime.UtcNow);
        request.MarkReceived(DateTime.UtcNow);
        await returns.AddAsync(request, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        return (order, request);
    }

    [Fact]
    public async Task HandleAsync_Should_MarkRequestRefunded_And_OrderReturned_When_Received()
    {
        var (db, orders, returns) = BuildScope();
        var (order, request) = await SeedReceivedRequestAsync(db, orders, returns);

        var handler = new OrderRefundedIntegrationEventHandler(returns, orders, db, TimeProvider.System);
        await handler.HandleAsync(new OrderRefundedIntegrationEvent(order.Id, order.UserId, request.Id, order.Total, "COD"), CancellationToken.None);

        var updatedRequest = await returns.GetByIdAsync(request.Id);
        updatedRequest!.Status.Should().Be(ReturnRequestStatus.Refunded);
        updatedRequest.RefundMethod.Should().Be("COD");

        var updatedOrder = await orders.GetByIdAsync(order.Id);
        updatedOrder!.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public async Task HandleAsync_Should_BeNoOp_When_AlreadyRefunded()
    {
        var (db, orders, returns) = BuildScope();
        var (order, request) = await SeedReceivedRequestAsync(db, orders, returns);

        var handler = new OrderRefundedIntegrationEventHandler(returns, orders, db, TimeProvider.System);
        var @event = new OrderRefundedIntegrationEvent(order.Id, order.UserId, request.Id, order.Total, "COD");
        await handler.HandleAsync(@event, CancellationToken.None);

        // Redelivery of the same event must not throw and must leave state unchanged.
        await handler.HandleAsync(@event, CancellationToken.None);

        var updatedRequest = await returns.GetByIdAsync(request.Id);
        updatedRequest!.Status.Should().Be(ReturnRequestStatus.Refunded);
    }

    [Fact]
    public async Task HandleAsync_Should_BeNoOp_When_RequestDoesNotExist()
    {
        var (db, orders, returns) = BuildScope();

        var handler = new OrderRefundedIntegrationEventHandler(returns, orders, db, TimeProvider.System);
        await handler.HandleAsync(new OrderRefundedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100000m, "COD"), CancellationToken.None);
    }
}

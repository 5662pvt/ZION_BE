using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Application.Features.MarkShippedBack;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class MarkShippedBackCommandHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db), new ReturnRequestRepository(db));
    }

    private static async Task<(Order order, Guid userId, ReturnRequest request)> SeedApprovedRequestAsync(OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns)
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
        await returns.AddAsync(request, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        return (order, userId, request);
    }

    [Fact]
    public async Task Handle_Should_MarkShippedBack_And_QueueOutboxEvent_When_OwnerAndApproved()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, request) = await SeedApprovedRequestAsync(db, orders, returns);

        var handler = new MarkShippedBackCommandHandler(returns, orders, db, TimeProvider.System);
        var result = await handler.Handle(new MarkShippedBackCommand(request.Id, userId, "VN999"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("ShippedBack");
        result.Value.ReturnTrackingCode.Should().Be("VN999");

        var outboxMessage = await db.Set<OutboxMessage>().SingleAsync();
        outboxMessage.Type.Should().Contain("OrderReturnShippedBackIntegrationEvent");
        outboxMessage.Content.Should().Contain(order.Id.ToString());
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_RequesterIsNotOwner()
    {
        var (db, orders, returns) = BuildScope();
        var (_, _, request) = await SeedApprovedRequestAsync(db, orders, returns);

        var handler = new MarkShippedBackCommandHandler(returns, orders, db, TimeProvider.System);
        var result = await handler.Handle(new MarkShippedBackCommand(request.Id, Guid.NewGuid(), null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnRequestNotFound");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_NotApprovedYet()
    {
        var (db, orders, returns) = BuildScope();
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
        await returns.AddAsync(request, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new MarkShippedBackCommandHandler(returns, orders, db, TimeProvider.System);
        var result = await handler.Handle(new MarkShippedBackCommand(request.Id, userId, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }
}

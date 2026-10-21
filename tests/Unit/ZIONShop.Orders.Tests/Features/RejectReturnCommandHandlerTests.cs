using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Application.Features.RejectReturn;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class RejectReturnCommandHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db), new ReturnRequestRepository(db));
    }

    private static async Task<(Order order, ReturnRequest request)> SeedRequestedAsync(OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns)
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

        var request = ReturnRequest.Create(order.Id, userId, ReturnReason.ChangedMind, null, order.Total, DateTime.UtcNow).Value;
        await returns.AddAsync(request, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        return (order, request);
    }

    [Fact]
    public async Task Handle_Should_Reject_And_QueueOutboxEvent_When_Requested()
    {
        var (db, orders, returns) = BuildScope();
        var (_, request) = await SeedRequestedAsync(db, orders, returns);

        var handler = new RejectReturnCommandHandler(returns, orders, db, TimeProvider.System);
        var result = await handler.Handle(new RejectReturnCommand(request.Id, "Đổi ý, không phải lỗi sản phẩm"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Rejected");
        result.Value.RejectReason.Should().Be("Đổi ý, không phải lỗi sản phẩm");

        var outboxMessage = await db.Set<OutboxMessage>().SingleAsync();
        outboxMessage.Content.Should().Contain("\"Approved\":false");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_RequestDoesNotExist()
    {
        var (db, orders, returns) = BuildScope();

        var handler = new RejectReturnCommandHandler(returns, orders, db, TimeProvider.System);
        var result = await handler.Handle(new RejectReturnCommand(Guid.NewGuid(), "Lý do"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnRequestNotFound");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_AlreadyApproved()
    {
        var (db, orders, returns) = BuildScope();
        var (_, request) = await SeedRequestedAsync(db, orders, returns);
        request.Approve(DateTime.UtcNow);
        returns.Update(request);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RejectReturnCommandHandler(returns, orders, db, TimeProvider.System);
        var result = await handler.Handle(new RejectReturnCommand(request.Id, "Lý do"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnInvalidTransition");
    }
}

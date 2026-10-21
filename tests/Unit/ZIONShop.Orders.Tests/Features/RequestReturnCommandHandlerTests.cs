using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Orders.Application.Features.RequestReturn;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;
using ZIONShop.Orders.Tests.Fakes;

namespace ZIONShop.Orders.Tests.Features;

public class RequestReturnCommandHandlerTests
{
    private static IOptions<ReturnsOptions> DefaultOptions() => Options.Create(new ReturnsOptions { WindowDays = 7 });

    private static (OrdersDbContext db, OrderRepository orders, ReturnRequestRepository returns) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db), new ReturnRequestRepository(db));
    }

    private static async Task<(Order order, Guid userId, DateTime completedAt)> SeedCompletedOrderAsync(OrdersDbContext db, OrderRepository orders)
    {
        var userId = Guid.NewGuid();
        var order = Order.Place(
            userId, PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        order.ConfirmReservation();
        order.Ship();
        order.Complete();
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var completedAt = order.StatusHistory.Single(h => h.Status == OrderStatus.Completed).At;
        return (order, userId, completedAt);
    }

    [Fact]
    public async Task Handle_Should_CreateRequest_When_OrderCompletedWithinWindow()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, _) = await SeedCompletedOrderAsync(db, orders);

        var handler = new RequestReturnCommandHandler(orders, returns, db, TimeProvider.System, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, "Defective", "Bị lỗi"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Requested");
        result.Value.Reason.Should().Be("Defective");
        result.Value.RefundAmount.Should().Be(order.Total);
    }

    [Fact]
    public async Task Handle_Should_Succeed_When_ExactlyAtWindowBoundary()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, completedAt) = await SeedCompletedOrderAsync(db, orders);
        var fakeTime = new FakeTimeProvider(completedAt.AddDays(7));

        var handler = new RequestReturnCommandHandler(orders, returns, db, fakeTime, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, "ChangedMind", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_ReturnWindowExpired_When_PastWindowBoundary()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, completedAt) = await SeedCompletedOrderAsync(db, orders);
        var fakeTime = new FakeTimeProvider(completedAt.AddDays(8));

        var handler = new RequestReturnCommandHandler(orders, returns, db, fakeTime, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, "ChangedMind", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnWindowExpired");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotEligible_When_OrderNotCompleted()
    {
        var (db, orders, returns) = BuildScope();
        var userId = Guid.NewGuid();
        var order = Order.Place(
            userId, PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        order.ConfirmReservation();
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RequestReturnCommandHandler(orders, returns, db, TimeProvider.System, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, "Other", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.ReturnNotEligible");
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_RequesterIsNotOwner()
    {
        var (db, orders, returns) = BuildScope();
        var (order, _, _) = await SeedCompletedOrderAsync(db, orders);

        var handler = new RequestReturnCommandHandler(orders, returns, db, TimeProvider.System, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, Guid.NewGuid(), "Other", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.OrderNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidReturnReason_When_ReasonIsNotARecognizedEnumValue()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, _) = await SeedCompletedOrderAsync(db, orders);

        var handler = new RequestReturnCommandHandler(orders, returns, db, TimeProvider.System, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, "NotARealReason", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidReturnReason");
    }

    [Fact]
    public async Task Handle_Should_ReturnAlreadyRequested_When_ActiveRequestExists()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, _) = await SeedCompletedOrderAsync(db, orders);

        var handler = new RequestReturnCommandHandler(orders, returns, db, TimeProvider.System, DefaultOptions());
        var first = await handler.Handle(new RequestReturnCommand(order.Id, userId, "Defective", null), CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        var second = await handler.Handle(new RequestReturnCommand(order.Id, userId, "Other", null), CancellationToken.None);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("Orders.ReturnAlreadyRequested");
    }

    [Fact]
    public async Task Handle_Should_AllowNewRequest_When_PreviousRequestWasRejected()
    {
        var (db, orders, returns) = BuildScope();
        var (order, userId, _) = await SeedCompletedOrderAsync(db, orders);

        var rejected = ReturnRequest.Create(order.Id, userId, ReturnReason.Other, null, order.Total, DateTime.UtcNow).Value;
        rejected.Reject("Quá thời hạn", DateTime.UtcNow);
        await returns.AddAsync(rejected, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new RequestReturnCommandHandler(orders, returns, db, TimeProvider.System, DefaultOptions());
        var result = await handler.Handle(new RequestReturnCommand(order.Id, userId, "Defective", null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}

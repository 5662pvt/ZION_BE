using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Application.Features.CancelOrder;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class CancelOrderCommandHandlerTests
{
    private static (OrdersDbContext db, OrderRepository orders) BuildScope()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        return (db, new OrderRepository(db));
    }

    private static async Task<(Order order, Guid userId)> SeedOrderAsync(OrdersDbContext db, OrderRepository orders, PaymentMethod paymentMethod = PaymentMethod.COD)
    {
        var userId = Guid.NewGuid();
        var order = Order.Place(
            userId, paymentMethod, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync(CancellationToken.None);
        return (order, userId);
    }

    [Fact]
    public async Task Handle_Should_Cancel_When_OwnerCancelsPendingOrder()
    {
        var (db, orders) = BuildScope();
        var (order, userId) = await SeedOrderAsync(db, orders);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, userId, false, "Đổi ý"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Handle_Should_DefaultCustomerReason_When_OwnerCancelsWithNoReason()
    {
        var (db, orders) = BuildScope();
        var (order, userId) = await SeedOrderAsync(db, orders);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, userId, false, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CancelReason.Should().Be("Khách hủy đơn");
        order.StatusHistory.Last().Note.Should().Be("Khách hủy đơn");
    }

    [Fact]
    public async Task Handle_Should_DefaultStaffReason_When_StaffCancelsWithBlankReason()
    {
        var (db, orders) = BuildScope();
        var (order, _) = await SeedOrderAsync(db, orders);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), true, "   "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CancelReason.Should().Be("Cửa hàng hủy đơn");
        order.StatusHistory.Last().Note.Should().Be("Cửa hàng hủy đơn");
    }

    [Fact]
    public async Task Handle_Should_TrimAndCapReason_When_ReasonExceedsMaxLength()
    {
        var (db, orders) = BuildScope();
        var (order, userId) = await SeedOrderAsync(db, orders);
        var longReason = "  " + new string('a', 600) + "  ";

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, userId, false, longReason), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CancelReason.Should().HaveLength(500);
        result.Value.CancelReason.Should().Be(new string('a', 500));
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_NonOwnerNonStaffCancels()
    {
        var (db, orders) = BuildScope();
        var (order, _) = await SeedOrderAsync(db, orders);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), false, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.OrderNotFound");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_OwnerCancelsAfterPaid()
    {
        var (db, orders) = BuildScope();
        var (order, userId) = await SeedOrderAsync(db, orders, PaymentMethod.Online);
        order.ConfirmReservation();
        order.CompletePayment();
        orders.Update(order);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, userId, false, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.CancelNotAllowed");
    }

    [Fact]
    public async Task Handle_Should_ReturnFailure_When_StaffCancelsAfterShipped()
    {
        var (db, orders) = BuildScope();
        var (order, _) = await SeedOrderAsync(db, orders);
        order.ConfirmReservation();
        order.Ship();
        orders.Update(order);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), true, "Quá muộn"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.CancelNotAllowed");
    }

    [Fact]
    public async Task Handle_Should_Cancel_When_StaffCancelsPaidOrder()
    {
        var (db, orders) = BuildScope();
        var (order, _) = await SeedOrderAsync(db, orders, PaymentMethod.Online);
        order.ConfirmReservation();
        order.CompletePayment();
        orders.Update(order);
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new CancelOrderCommandHandler(orders, db);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, Guid.NewGuid(), true, "Staff hủy"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
    }
}

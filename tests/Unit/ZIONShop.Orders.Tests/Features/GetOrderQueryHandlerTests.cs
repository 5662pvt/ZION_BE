using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Application.Features.GetOrder;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class GetOrderQueryHandlerTests
{
    private static async Task<(OrderRepository orders, Order seeded, Guid userId)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        var orders = new OrderRepository(db);
        var userId = Guid.NewGuid();

        var order = Order.Place(
            userId, PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        order.ConfirmReservation();

        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync();

        return (orders, order, userId);
    }

    [Fact]
    public async Task Handle_Should_ReturnHistoryOrderedAscendingByAt_When_OrderHasMultipleTransitions()
    {
        var (orders, seeded, userId) = await SeedAsync();
        var handler = new GetOrderQueryHandler(orders);

        var result = await handler.Handle(new GetOrderQuery(seeded.Id, userId, IsStaffOrAdmin: false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.History.Should().HaveCount(2);
        result.Value.History.Select(h => h.Status).Should().ContainInOrder("PendingStock", "Confirmed");
        result.Value.History.Should().BeInAscendingOrder(h => h.At);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_OtherUserRequestsOrder()
    {
        var (orders, seeded, _) = await SeedAsync();
        var handler = new GetOrderQueryHandler(orders);

        var result = await handler.Handle(new GetOrderQuery(seeded.Id, Guid.NewGuid(), IsStaffOrAdmin: false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}

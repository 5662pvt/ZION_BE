using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Application.Features.GetMyOrders;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class GetMyOrdersQueryHandlerTests
{
    private static async Task<(OrderRepository orders, Guid userId, Order inRange, Order beforeRange, Order afterRange)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        var orders = new OrderRepository(db);
        var userId = Guid.NewGuid();

        Order MakeOrder() => Order.Place(
            userId, PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 1) },
            null);

        // VN day 2026-06-15 == UTC [2026-06-14 17:00, 2026-06-15 17:00)
        var inRange = MakeOrder();
        inRange.CreatedDate = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);

        var beforeRange = MakeOrder();
        beforeRange.CreatedDate = new DateTime(2026, 6, 14, 10, 0, 0, DateTimeKind.Utc);

        var afterRange = MakeOrder();
        afterRange.CreatedDate = new DateTime(2026, 6, 15, 18, 0, 0, DateTimeKind.Utc);

        await orders.AddAsync(inRange, CancellationToken.None);
        await orders.AddAsync(beforeRange, CancellationToken.None);
        await orders.AddAsync(afterRange, CancellationToken.None);
        await db.SaveChangesAsync();

        return (orders, userId, inRange, beforeRange, afterRange);
    }

    [Fact]
    public async Task Handle_Should_IncludeOnlyOrdersWithinVnDay_When_FromEqualsToSameDay()
    {
        var (orders, userId, inRange, _, _) = await SeedAsync();
        var handler = new GetMyOrdersQueryHandler(orders);

        var result = await handler.Handle(new GetMyOrdersQuery(userId, 1, 20, null, "2026-06-15", "2026-06-15"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(o => o.Id == inRange.Id);
    }

    [Fact]
    public async Task Handle_Should_ExcludeOrders_OutsideVnDayRange()
    {
        var (orders, userId, _, beforeRange, afterRange) = await SeedAsync();
        var handler = new GetMyOrdersQueryHandler(orders);

        var result = await handler.Handle(new GetMyOrdersQuery(userId, 1, 20, null, "2026-06-15", "2026-06-15"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().NotContain(o => o.Id == beforeRange.Id || o.Id == afterRange.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnBadRequest_When_FromIsAfterTo()
    {
        var (orders, userId, _, _, _) = await SeedAsync();
        var handler = new GetMyOrdersQueryHandler(orders);

        var result = await handler.Handle(new GetMyOrdersQuery(userId, 1, 20, null, "2026-06-15", "2026-06-10"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidDateRange");
    }

    [Fact]
    public async Task Handle_Should_ReturnBadRequest_When_RangeExceeds366Days()
    {
        var (orders, userId, _, _, _) = await SeedAsync();
        var handler = new GetMyOrdersQueryHandler(orders);

        var result = await handler.Handle(new GetMyOrdersQuery(userId, 1, 20, null, "2025-01-01", "2026-06-15"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidDateRange");
    }

    [Fact]
    public async Task Handle_Should_ReturnBadRequest_When_FormatIsInvalid()
    {
        var (orders, userId, _, _, _) = await SeedAsync();
        var handler = new GetMyOrdersQueryHandler(orders);

        var result = await handler.Handle(new GetMyOrdersQuery(userId, 1, 20, null, "15/06/2026", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidDateRange");
    }
}

using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Application.Features.SearchOrders;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.Orders.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Repositories;

namespace ZIONShop.Orders.Tests.Features;

public class SearchOrdersQueryHandlerTests
{
    private static async Task<(OrderRepository orders, Order seeded)> SeedAsync()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new OrdersDbContext(options);
        var orders = new OrderRepository(db);

        var order = Order.Place(
            Guid.NewGuid(), PaymentMethod.COD, "VND", 30000,
            new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
            new[] { (Guid.NewGuid(), "Widget", 100000m, 1) },
            null);
        await orders.AddAsync(order, CancellationToken.None);
        await db.SaveChangesAsync();

        return (orders, order);
    }

    [Fact]
    public async Task Handle_Should_FindOrder_When_KeywordCaseDiffersFromStoredCode()
    {
        var (orders, seeded) = await SeedAsync();
        var handler = new SearchOrdersQueryHandler(orders);

        var result = await handler.Handle(new SearchOrdersQuery(1, 20, null, null, null, seeded.Code.ToLowerInvariant()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(o => o.Code == seeded.Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_When_KeywordDoesNotMatch()
    {
        var (orders, _) = await SeedAsync();
        var handler = new SearchOrdersQueryHandler(orders);

        var result = await handler.Handle(new SearchOrdersQuery(1, 20, null, null, null, "NO-SUCH-CODE"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_FindOrder_When_KeywordMatchesPhoneNumber()
    {
        var (orders, seeded) = await SeedAsync();
        var handler = new SearchOrdersQueryHandler(orders);

        var result = await handler.Handle(new SearchOrdersQuery(1, 20, null, null, null, "0900000000"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(o => o.Code == seeded.Code);
    }

    [Fact]
    public async Task Handle_Should_FindOrder_When_KeywordMatchesRecipientNameCaseInsensitive()
    {
        var (orders, seeded) = await SeedAsync();
        var handler = new SearchOrdersQueryHandler(orders);

        var result = await handler.Handle(new SearchOrdersQuery(1, 20, null, null, null, "nguyen van a"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(o => o.Code == seeded.Code);
        result.Value.Items.Single().RecipientName.Should().Be("Nguyen Van A");
        result.Value.Items.Single().PhoneNumber.Should().Be("0900000000");
    }
}

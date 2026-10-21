using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Orders;
using ZIONShop.Notifications.Application.IntegrationEvents;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.IntegrationEvents;

public class OrderReturnRequestedAdminNotificationHandlerTests
{
    private static (NotificationsDbContext db, UserNotificationRepository userNotifications) BuildScope()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new NotificationsDbContext(options);
        return (db, new UserNotificationRepository(db));
    }

    private static OrderDetails BuildOrder(Guid orderId, Guid userId) => new(
        orderId, userId, "ZS0010", "Delivered", "COD",
        200000m, 0, null, null, 30000m, 230000m, "VND",
        "Nguyen Van A", "0900000000", "123 Đường ABC, Cầu Giấy, Hà Nội", null, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget", 2, 100000m, 200000m) });

    [Fact]
    public async Task HandleAsync_Should_CreateNotificationForEachStaffUserAndPush_When_OrderExists()
    {
        var (db, userNotifications) = BuildScope();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var returnRequestId = Guid.NewGuid();
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, customerId));
        var staff = new FakeStaffUserReader().With(admin1, admin2);
        var push = new FakeNotificationPush();

        var handler = new OrderReturnRequestedAdminNotificationHandler(userNotifications, db, orders, staff, push, NullLogger<OrderReturnRequestedAdminNotificationHandler>.Instance);
        await handler.HandleAsync(new OrderReturnRequestedIntegrationEvent(orderId, customerId, returnRequestId, "Sản phẩm bị lỗi"));

        userNotifications.QueryForUser(admin1).Should().ContainSingle(n => n.Type == UserNotificationType.ReturnRequested && n.OrderId == orderId);
        userNotifications.QueryForUser(admin2).Should().ContainSingle(n => n.Type == UserNotificationType.ReturnRequested && n.OrderId == orderId);
        push.Published.Should().HaveCount(2);
        push.Published.Should().Contain(p => p.UserId == admin1);
        push.Published.Should().Contain(p => p.UserId == admin2);
    }

    [Fact]
    public async Task HandleAsync_Should_SkipGracefully_When_OrderNotFound()
    {
        var (db, userNotifications) = BuildScope();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var staff = new FakeStaffUserReader().With(Guid.NewGuid());
        var push = new FakeNotificationPush();

        var handler = new OrderReturnRequestedAdminNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), staff, push, NullLogger<OrderReturnRequestedAdminNotificationHandler>.Instance);
        var act = async () => await handler.HandleAsync(new OrderReturnRequestedIntegrationEvent(orderId, customerId, Guid.NewGuid(), "Sản phẩm bị lỗi"));

        await act.Should().NotThrowAsync();
        push.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_And_StillPushOthers_When_OnePushFails()
    {
        var (db, userNotifications) = BuildScope();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, customerId));
        var staff = new FakeStaffUserReader().With(admin1, admin2);
        var push = new FakeNotificationPush { ShouldThrow = true };

        var handler = new OrderReturnRequestedAdminNotificationHandler(userNotifications, db, orders, staff, push, NullLogger<OrderReturnRequestedAdminNotificationHandler>.Instance);
        var act = async () => await handler.HandleAsync(new OrderReturnRequestedIntegrationEvent(orderId, customerId, Guid.NewGuid(), "Sản phẩm bị lỗi"));

        await act.Should().NotThrowAsync();
        userNotifications.QueryForUser(admin1).Should().ContainSingle();
        userNotifications.QueryForUser(admin2).Should().ContainSingle();
    }
}

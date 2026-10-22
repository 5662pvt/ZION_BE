using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.Contracts.Orders;
using ZIONShop.Notifications.Application.IntegrationEvents;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.IntegrationEvents;

public class LoyaltyPointsReturnedNotificationHandlerTests
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
        orderId, userId, "ZS0005", "Cancelled", "Online",
        200000m, 0, null, null, 30000m, 230000m, "VND",
        "Nguyen Van A", "0900000000", "123 Đường ABC, Cầu Giấy, Hà Nội", null, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget", 2, 100000m, 200000m) });

    [Fact]
    public async Task HandleAsync_Should_UseOrderCode_When_OrderResolves()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush();

        var handler = new LoyaltyPointsReturnedNotificationHandler(userNotifications, db, orders, push, NullLogger<LoyaltyPointsReturnedNotificationHandler>.Instance);
        await handler.HandleAsync(new LoyaltyPointsReturnedIntegrationEvent(userId, 100, orderId, "OrderCancelled"));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Type.Should().Be(UserNotificationType.PointsReturned);
        notification.OrderId.Should().Be(orderId);
        notification.Message.Should().Contain("ZS0005");
        notification.Message.Should().Contain("huỷ");
        push.Published.Should().ContainSingle(p => p.UserId == userId && p.Dto.Type == "PointsReturned");
    }

    [Fact]
    public async Task HandleAsync_Should_FallBackToShortOrderId_When_OrderNotFound()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var push = new FakeNotificationPush();

        var handler = new LoyaltyPointsReturnedNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), push, NullLogger<LoyaltyPointsReturnedNotificationHandler>.Instance);
        await handler.HandleAsync(new LoyaltyPointsReturnedIntegrationEvent(userId, 100, orderId, "OrderRefunded"));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Message.Should().Contain(orderId.ToString()[..8]);
        notification.Message.Should().Contain("hoàn tiền");
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_When_PushFails()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush { ShouldThrow = true };

        var handler = new LoyaltyPointsReturnedNotificationHandler(userNotifications, db, orders, push, NullLogger<LoyaltyPointsReturnedNotificationHandler>.Instance);
        var act = async () => await handler.HandleAsync(new LoyaltyPointsReturnedIntegrationEvent(userId, 100, orderId, "OrderCancelled"));

        await act.Should().NotThrowAsync();
        userNotifications.QueryForUser(userId).Should().ContainSingle();
    }
}

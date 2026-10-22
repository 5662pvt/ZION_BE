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

public class LoyaltyPointsEarnedNotificationHandlerTests
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
        orderId, userId, "ZS0005", "Completed", "Online",
        200000m, 0, null, null, 30000m, 230000m, "VND",
        "Nguyen Van A", "0900000000", "123 Đường ABC, Cầu Giấy, Hà Nội", null, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget", 2, 100000m, 200000m) });

    [Fact]
    public async Task HandleAsync_Should_UseOrderCode_When_SourceIsOrderAndOrderResolves()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush();

        var handler = new LoyaltyPointsEarnedNotificationHandler(userNotifications, db, orders, push, NullLogger<LoyaltyPointsEarnedNotificationHandler>.Instance);
        await handler.HandleAsync(new LoyaltyPointsEarnedIntegrationEvent(userId, 100, "Order", orderId, null, DateTime.UtcNow.AddYears(1)));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Type.Should().Be(UserNotificationType.PointsEarned);
        notification.OrderId.Should().Be(orderId);
        notification.Message.Should().Contain("ZS0005");
        push.Published.Should().ContainSingle(p => p.UserId == userId && p.Dto.Type == "PointsEarned");
    }

    [Fact]
    public async Task HandleAsync_Should_FallBackToShortOrderId_When_SourceIsOrderAndOrderNotFound()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var push = new FakeNotificationPush();

        var handler = new LoyaltyPointsEarnedNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), push, NullLogger<LoyaltyPointsEarnedNotificationHandler>.Instance);
        await handler.HandleAsync(new LoyaltyPointsEarnedIntegrationEvent(userId, 100, "Order", orderId, null, DateTime.UtcNow.AddYears(1)));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Message.Should().Contain(orderId.ToString()[..8]);
    }

    [Fact]
    public async Task HandleAsync_Should_UseReviewTemplate_When_SourceIsReview()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var push = new FakeNotificationPush();

        var handler = new LoyaltyPointsEarnedNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), push, NullLogger<LoyaltyPointsEarnedNotificationHandler>.Instance);
        await handler.HandleAsync(new LoyaltyPointsEarnedIntegrationEvent(userId, 100, "Review", null, productId, DateTime.UtcNow.AddYears(1)));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.OrderId.Should().BeNull();
        notification.Message.Should().Contain("đánh giá sản phẩm");
    }

    [Fact]
    public async Task HandleAsync_Should_UseAdminAdjustTemplate_When_SourceIsAdminAdjust()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var push = new FakeNotificationPush();

        var handler = new LoyaltyPointsEarnedNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), push, NullLogger<LoyaltyPointsEarnedNotificationHandler>.Instance);
        await handler.HandleAsync(new LoyaltyPointsEarnedIntegrationEvent(userId, 50, "AdminAdjust", null, null, DateTime.UtcNow.AddYears(1)));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Title.Should().Be("Bạn được cộng Xu");
        notification.Message.Should().Contain("50 Xu");
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_When_PushFails()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var push = new FakeNotificationPush { ShouldThrow = true };

        var handler = new LoyaltyPointsEarnedNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), push, NullLogger<LoyaltyPointsEarnedNotificationHandler>.Instance);
        var act = async () => await handler.HandleAsync(new LoyaltyPointsEarnedIntegrationEvent(userId, 100, "AdminAdjust", null, null, DateTime.UtcNow.AddYears(1)));

        await act.Should().NotThrowAsync();
        userNotifications.QueryForUser(userId).Should().ContainSingle();
    }
}

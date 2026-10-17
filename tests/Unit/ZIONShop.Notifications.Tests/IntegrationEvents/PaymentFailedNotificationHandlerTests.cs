using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Payments;
using ZIONShop.Notifications.Application.IntegrationEvents;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.IntegrationEvents;

public class PaymentFailedNotificationHandlerTests
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
        orderId, userId, "ZS0006", "PendingPayment", "Online",
        200000m, 0, null, null, 30000m, 230000m, "VND",
        "Nguyen Van A", "0900000000", "123 Đường ABC, Cầu Giấy, Hà Nội", null, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget", 2, 100000m, 200000m) });

    [Fact]
    public async Task HandleAsync_Should_CreateUserNotificationAndPush_When_OrderResolves()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush();

        var handler = new PaymentFailedNotificationHandler(userNotifications, db, orders, push, NullLogger<PaymentFailedNotificationHandler>.Instance);
        await handler.HandleAsync(new PaymentFailedIntegrationEvent(orderId, Guid.NewGuid(), "Insufficient funds"));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Type.Should().Be(UserNotificationType.PaymentFailed);
        notification.OrderId.Should().Be(orderId);
        push.Published.Should().ContainSingle(p => p.UserId == userId && p.Dto.Type == "PaymentFailed");
    }

    [Fact]
    public async Task HandleAsync_Should_SkipGracefully_When_OrderNotFound()
    {
        var (db, userNotifications) = BuildScope();
        var push = new FakeNotificationPush();

        var handler = new PaymentFailedNotificationHandler(userNotifications, db, new FakeOrderDetailsReader(), push, NullLogger<PaymentFailedNotificationHandler>.Instance);
        var act = async () => await handler.HandleAsync(new PaymentFailedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), "Insufficient funds"));

        await act.Should().NotThrowAsync();
        push.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_When_PushFails()
    {
        var (db, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush { ShouldThrow = true };

        var handler = new PaymentFailedNotificationHandler(userNotifications, db, orders, push, NullLogger<PaymentFailedNotificationHandler>.Instance);
        var act = async () => await handler.HandleAsync(new PaymentFailedIntegrationEvent(orderId, Guid.NewGuid(), "Insufficient funds"));

        await act.Should().NotThrowAsync();
        userNotifications.QueryForUser(userId).Should().ContainSingle();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Orders;
using ZIONShop.Notifications.Application.IntegrationEvents;
using ZIONShop.Notifications.Application.Options;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.IntegrationEvents;

public class OrderCancelledIntegrationEventHandlerTests
{
    private static (NotificationsDbContext db, NotificationLogRepository logs, UserNotificationRepository userNotifications) BuildScope()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new NotificationsDbContext(options);
        return (db, new NotificationLogRepository(db), new UserNotificationRepository(db));
    }

    private static IOptions<NotificationsOptions> DefaultOptions() =>
        Options.Create(new NotificationsOptions { StorefrontBaseUrl = "http://localhost:3000" });

    private static OrderDetails BuildOrder(Guid orderId, Guid userId) => new(
        orderId, userId, "ZS0003", "Cancelled", "COD",
        200000m, 0, null, null, 30000m, 230000m, "VND",
        "Nguyen Van A", "0900000000", "123 Đường ABC, Cầu Giấy, Hà Nội", null, DateTime.UtcNow,
        new[] { new OrderDetailsLine("Widget", 2, 100000m, 200000m) });

    private static OrderCancelledIntegrationEventHandler BuildHandler(
        NotificationLogRepository logs, UserNotificationRepository userNotifications, NotificationsDbContext db,
        FakeUserContactReader contacts, FakeOrderDetailsReader orders, FakeEmailSender email, FakeNotificationPush push) =>
        new(logs, userNotifications, db, contacts, orders, email, push, DefaultOptions(), NullLogger<OrderCancelledIntegrationEventHandler>.Instance);

    [Fact]
    public async Task HandleAsync_Should_CreateUserNotificationAndPush_When_EmailSentSuccessfully()
    {
        var (db, logs, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local");
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush();

        var handler = BuildHandler(logs, userNotifications, db, contacts, orders, new FakeEmailSender(), push);
        await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, userId, "Customer requested"));

        var notification = userNotifications.QueryForUser(userId).Single();
        notification.Type.Should().Be(UserNotificationType.OrderCancelled);
        notification.OrderId.Should().Be(orderId);
        push.Published.Should().ContainSingle(p => p.UserId == userId && p.Dto.Type == "OrderCancelled");
    }

    [Fact]
    public async Task HandleAsync_Should_NotThrow_When_PushFails()
    {
        var (db, logs, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local");
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush { ShouldThrow = true };

        var handler = BuildHandler(logs, userNotifications, db, contacts, orders, new FakeEmailSender(), push);
        var act = async () => await handler.HandleAsync(new OrderCancelledIntegrationEvent(orderId, userId, "Customer requested"));

        await act.Should().NotThrowAsync();
        userNotifications.QueryForUser(userId).Should().ContainSingle();
    }

    [Fact]
    public async Task HandleAsync_Should_BeIdempotent_When_CalledTwiceForSameEvent()
    {
        var (db, logs, userNotifications) = BuildScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var contacts = new FakeUserContactReader().With(userId, "customer@zionshop.local");
        var orders = new FakeOrderDetailsReader().With(BuildOrder(orderId, userId));
        var push = new FakeNotificationPush();

        var handler = BuildHandler(logs, userNotifications, db, contacts, orders, new FakeEmailSender(), push);
        var @event = new OrderCancelledIntegrationEvent(orderId, userId, "Customer requested");

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        logs.Query().Count().Should().Be(1);
        userNotifications.QueryForUser(userId).Should().ContainSingle();
    }
}

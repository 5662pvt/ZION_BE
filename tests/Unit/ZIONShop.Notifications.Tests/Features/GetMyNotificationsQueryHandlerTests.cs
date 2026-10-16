using Microsoft.EntityFrameworkCore;
using ZIONShop.Notifications.Application.Features.GetMyNotifications;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Notifications.Infrastructure.Repositories;

namespace ZIONShop.Notifications.Tests.Features;

public class GetMyNotificationsQueryHandlerTests
{
    private static (NotificationsDbContext db, NotificationLogRepository logs) BuildScope()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new NotificationsDbContext(options);
        return (db, new NotificationLogRepository(db));
    }

    [Fact]
    public async Task Handle_Should_ReturnOnlyCurrentUsersLogs_NewestFirst()
    {
        var (db, logs) = BuildScope();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var older = NotificationLog.Sent(Guid.NewGuid(), NotificationType.OrderConfirmed, userId, orderId, "a@b.com", "Order confirmed", DateTime.UtcNow.AddMinutes(-10));
        var newer = NotificationLog.Sent(Guid.NewGuid(), NotificationType.OrderShipped, userId, orderId, "a@b.com", "Order shipped", DateTime.UtcNow);
        var others = NotificationLog.Sent(Guid.NewGuid(), NotificationType.OrderConfirmed, otherUserId, Guid.NewGuid(), "c@d.com", "Order confirmed", DateTime.UtcNow);

        await db.NotificationLogs.AddRangeAsync(older, newer, others);
        await db.SaveChangesAsync();

        var handler = new GetMyNotificationsQueryHandler(logs);
        var result = await handler.Handle(new GetMyNotificationsQuery(userId, 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(2);
        result.Value.Items[0].Id.Should().Be(newer.Id);
        result.Value.Items[1].Id.Should().Be(older.Id);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmpty_When_UserHasNoNotifications()
    {
        var (_, logs) = BuildScope();
        var handler = new GetMyNotificationsQueryHandler(logs);

        var result = await handler.Handle(new GetMyNotificationsQuery(Guid.NewGuid(), 1, 20), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }
}

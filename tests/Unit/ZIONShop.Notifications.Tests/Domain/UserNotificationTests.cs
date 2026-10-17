using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;

namespace ZIONShop.Notifications.Tests.Domain;

public class UserNotificationTests
{
    [Fact]
    public void Create_Should_SetFieldsAndDefaultUnread()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var notification = UserNotification.Create(userId, UserNotificationType.OrderConfirmed, "Title", "Message", orderId);

        notification.UserId.Should().Be(userId);
        notification.Type.Should().Be(UserNotificationType.OrderConfirmed);
        notification.Title.Should().Be("Title");
        notification.Message.Should().Be("Message");
        notification.OrderId.Should().Be(orderId);
        notification.IsRead.Should().BeFalse();
        notification.ReadAt.Should().BeNull();
        notification.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_Should_AllowNullOrderId()
    {
        var notification = UserNotification.Create(Guid.NewGuid(), UserNotificationType.OrderPlaced, "Title", "Message", orderId: null);

        notification.OrderId.Should().BeNull();
    }

    [Fact]
    public void MarkAsRead_Should_SetIsReadAndReadAt_When_Unread()
    {
        var notification = UserNotification.Create(Guid.NewGuid(), UserNotificationType.OrderPlaced, "Title", "Message", Guid.NewGuid());

        notification.MarkAsRead();

        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public void MarkAsRead_Should_BeIdempotent_When_AlreadyRead()
    {
        var notification = UserNotification.Create(Guid.NewGuid(), UserNotificationType.OrderPlaced, "Title", "Message", Guid.NewGuid());
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;

        notification.MarkAsRead();

        notification.ReadAt.Should().Be(firstReadAt);
    }
}

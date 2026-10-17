using ZIONShop.Notifications.Application.Features.MarkAllNotificationsRead;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.Features;

public class MarkAllNotificationsReadCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_MarkAllUnreadForCaller_And_ReturnUpdatedCount()
    {
        var userId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var alreadyRead = UserNotification.Create(userId, UserNotificationType.OrderPlaced, "A", "msg", Guid.NewGuid());
        alreadyRead.MarkAsRead();
        var repo = new FakeUserNotificationRepository()
            .With(alreadyRead)
            .With(UserNotification.Create(userId, UserNotificationType.OrderPlaced, "B", "msg", Guid.NewGuid()))
            .With(UserNotification.Create(userId, UserNotificationType.OrderPlaced, "C", "msg", Guid.NewGuid()))
            .With(UserNotification.Create(other, UserNotificationType.OrderPlaced, "D", "msg", Guid.NewGuid()));

        var handler = new MarkAllNotificationsReadCommandHandler(repo);
        var result = await handler.Handle(new MarkAllNotificationsReadCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        repo.QueryForUser(userId).Should().OnlyContain(n => n.IsRead);
        repo.QueryForUser(other).Should().OnlyContain(n => !n.IsRead);
    }
}

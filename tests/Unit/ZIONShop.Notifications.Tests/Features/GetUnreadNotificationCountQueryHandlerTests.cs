using ZIONShop.Notifications.Application.Features.GetUnreadNotificationCount;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.Features;

public class GetUnreadNotificationCountQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_CountOnlyUnreadForCaller()
    {
        var userId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var read = UserNotification.Create(userId, UserNotificationType.OrderPlaced, "A", "msg", Guid.NewGuid());
        read.MarkAsRead();
        var repo = new FakeUserNotificationRepository()
            .With(read)
            .With(UserNotification.Create(userId, UserNotificationType.OrderPlaced, "B", "msg", Guid.NewGuid()))
            .With(UserNotification.Create(userId, UserNotificationType.OrderPlaced, "C", "msg", Guid.NewGuid()))
            .With(UserNotification.Create(other, UserNotificationType.OrderPlaced, "D", "msg", Guid.NewGuid()));

        var handler = new GetUnreadNotificationCountQueryHandler(repo);
        var result = await handler.Handle(new GetUnreadNotificationCountQuery(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }
}

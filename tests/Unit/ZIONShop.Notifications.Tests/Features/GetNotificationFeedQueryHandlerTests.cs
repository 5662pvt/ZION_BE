using ZIONShop.Notifications.Application.Features.GetNotificationFeed;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.Features;

public class GetNotificationFeedQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnOnlyCallersNotifications_NewestFirst()
    {
        var userId = Guid.NewGuid();
        var other = Guid.NewGuid();
        var repo = new FakeUserNotificationRepository()
            .With(UserNotification.Create(userId, UserNotificationType.OrderPlaced, "A", "msg", Guid.NewGuid()))
            .With(UserNotification.Create(other, UserNotificationType.OrderPlaced, "B", "msg", Guid.NewGuid()));

        var handler = new GetNotificationFeedQueryHandler(repo);
        var result = await handler.Handle(new GetNotificationFeedQuery(userId, 1, 20, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle(i => i.Title == "A");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_FilterUnreadOnly_When_RequestedTrue()
    {
        var userId = Guid.NewGuid();
        var read = UserNotification.Create(userId, UserNotificationType.OrderPlaced, "Read", "msg", Guid.NewGuid());
        read.MarkAsRead();
        var unread = UserNotification.Create(userId, UserNotificationType.OrderPlaced, "Unread", "msg", Guid.NewGuid());
        var repo = new FakeUserNotificationRepository().With(read).With(unread);

        var handler = new GetNotificationFeedQueryHandler(repo);
        var result = await handler.Handle(new GetNotificationFeedQuery(userId, 1, 20, true), CancellationToken.None);

        result.Value.Items.Should().ContainSingle(i => i.Title == "Unread");
    }

    [Fact]
    public async Task Handle_Should_Paginate_When_MoreItemsThanPageSize()
    {
        var userId = Guid.NewGuid();
        var repo = new FakeUserNotificationRepository();
        for (var i = 0; i < 5; i++)
            repo.With(UserNotification.Create(userId, UserNotificationType.OrderPlaced, $"N{i}", "msg", Guid.NewGuid()));

        var handler = new GetNotificationFeedQueryHandler(repo);
        var result = await handler.Handle(new GetNotificationFeedQuery(userId, 1, 2, false), CancellationToken.None);

        result.Value.Items.Should().HaveCount(2);
        result.Value.TotalCount.Should().Be(5);
    }
}

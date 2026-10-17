using ZIONShop.Notifications.Application.Features.MarkNotificationRead;
using ZIONShop.Notifications.Application.Interfaces;
using ZIONShop.Notifications.Domain.Entities;
using ZIONShop.Notifications.Domain.Enums;
using ZIONShop.Notifications.Tests.Fakes;

namespace ZIONShop.Notifications.Tests.Features;

public class MarkNotificationReadCommandHandlerTests
{
    private sealed class NoopUnitOfWork : INotificationsUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    [Fact]
    public async Task Handle_Should_MarkAsRead_When_OwnedByCaller()
    {
        var userId = Guid.NewGuid();
        var notification = UserNotification.Create(userId, UserNotificationType.OrderPlaced, "A", "msg", Guid.NewGuid());
        var repo = new FakeUserNotificationRepository().With(notification);
        var uow = new NoopUnitOfWork();

        var handler = new MarkNotificationReadCommandHandler(repo, uow);
        var result = await handler.Handle(new MarkNotificationReadCommand(notification.Id, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_NotificationBelongsToAnotherUser()
    {
        var owner = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var notification = UserNotification.Create(owner, UserNotificationType.OrderPlaced, "A", "msg", Guid.NewGuid());
        var repo = new FakeUserNotificationRepository().With(notification);

        var handler = new MarkNotificationReadCommandHandler(repo, new NoopUnitOfWork());
        var result = await handler.Handle(new MarkNotificationReadCommand(notification.Id, someoneElse), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_ReturnNotFound_When_IdDoesNotExist()
    {
        var userId = Guid.NewGuid();
        var repo = new FakeUserNotificationRepository();

        var handler = new MarkNotificationReadCommandHandler(repo, new NoopUnitOfWork());
        var result = await handler.Handle(new MarkNotificationReadCommand(Guid.NewGuid(), userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}

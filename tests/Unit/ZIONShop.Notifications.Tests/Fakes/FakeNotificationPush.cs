using ZIONShop.Realtime;

namespace ZIONShop.Notifications.Tests.Fakes;

public class FakeNotificationPush : INotificationPush
{
    public List<(Guid UserId, UserNotificationPushDto Dto)> Published { get; } = new();
    public bool ShouldThrow { get; set; }

    public Task PublishAsync(Guid userId, UserNotificationPushDto notification, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated push failure");
        Published.Add((userId, notification));
        return Task.CompletedTask;
    }
}

namespace ZIONShop.Realtime;

/// <summary>
/// Fail-open by contract: a Redis outage must never throw out of a caller (an integration-event
/// handler persisting a <c>UserNotification</c>) or block/fail that handler's business transaction.
/// Implementations log a Warning and return instead of propagating.
/// </summary>
public interface INotificationPush
{
    Task PublishAsync(Guid userId, UserNotificationPushDto notification, CancellationToken cancellationToken = default);
}

namespace ZIONShop.Realtime;

/// <summary>
/// Generic server -> client real-time event publisher (distinct from <see cref="INotificationPush"/>,
/// which is specific to the persisted <c>UserNotification</c> feed). Fail-open by contract: a Redis
/// outage must never throw out of a caller or block/fail the business transaction that triggered the
/// event. Implementations log a Warning and return instead of propagating.
/// </summary>
public interface IRealtimeEventPublisher
{
    Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default);

    /// <summary>Broadcasts to every connected Staff/Admin connection (the shared inbox group).</summary>
    Task PublishToStaffAsync(string eventName, object payload, CancellationToken ct = default);
}

namespace ZIONShop.EventBus.Outbox;

/// <summary>Mirrors the <c>ProcessedOn</c>/<c>Attempts</c> classification <see cref="OutboxPublisherService{TDbContext}"/>
/// already applies when it reports the outbox.pending/outbox.failed gauges -- see
/// <see cref="OutboxPublisherService{TDbContext}.MaxAttempts"/>.</summary>
public enum OutboxStatus
{
    Pending,
    Failed,
    Processed
}

public record OutboxMessageSummary(
    Guid Id, string Module, string EventType, DateTime OccurredOn, DateTime? ProcessedOn,
    int Attempts, string Status, string? LastError);

public record OutboxMessageDetail(
    Guid Id, string Module, string EventType, DateTime OccurredOn, DateTime? ProcessedOn,
    int Attempts, string Status, string? LastError, string? TraceParent, string Payload);

public enum OutboxRetryOutcome
{
    NotFound,
    NotFailed,
    Retried
}

/// <summary>
/// Read/retry access to one module's outbox table for the admin "Hệ thống" screens, without the
/// Admin/API layer ever referencing that module's Infrastructure project directly. One instance is
/// registered per module DbContext (see <see cref="OutboxServiceCollectionExtensions.AddOutbox{TDbContext}"/>);
/// the controller aggregates every registered <see cref="IOutboxAdminStore"/> via <c>IEnumerable&lt;&gt;</c>.
/// </summary>
public interface IOutboxAdminStore
{
    string Module { get; }

    Task<int> CountAsync(OutboxStatus? status, CancellationToken cancellationToken);

    /// <summary>Newest <paramref name="take"/> rows (by <c>OccurredOn</c> descending) matching
    /// <paramref name="status"/>. Callers that need to merge several stores into one globally-sorted
    /// page only need each store's own top <paramref name="take"/> rows -- any row that could appear
    /// in the global top <paramref name="take"/> is necessarily within this store's own top
    /// <paramref name="take"/> too.</summary>
    Task<IReadOnlyList<OutboxMessageSummary>> ListNewestAsync(OutboxStatus? status, int take, CancellationToken cancellationToken);

    Task<OutboxMessageDetail?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<OutboxRetryOutcome> RetryAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Resets every currently-Failed row back to Pending. Returns how many rows were retried.</summary>
    Task<int> RetryFailedAsync(CancellationToken cancellationToken);
}

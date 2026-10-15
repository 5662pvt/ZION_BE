namespace ZIONShop.EventBus.Inbox;

public record InboxMessageSummary(Guid MessageId, string Module, string Consumer, string EventType, DateTime ProcessedOn);

/// <summary>
/// Read access to one module's inbox table for the admin "Hệ thống" screens, without the Admin/API
/// layer referencing that module's Infrastructure project directly. Registered per module DbContext
/// via <see cref="InboxServiceCollectionExtensions.AddInboxAdmin{TDbContext}"/>; the controller
/// aggregates every registered <see cref="IInboxAdminStore"/> via <c>IEnumerable&lt;&gt;</c>.
/// </summary>
public interface IInboxAdminStore
{
    string Module { get; }

    Task<int> CountAsync(CancellationToken cancellationToken);

    /// <summary>Newest <paramref name="take"/> rows by <c>ProcessedOn</c> descending -- see
    /// <see cref="Outbox.IOutboxAdminStore.ListNewestAsync"/> for why top-<paramref name="take"/> per
    /// store is enough to merge several stores into one globally-sorted page.</summary>
    Task<IReadOnlyList<InboxMessageSummary>> ListNewestAsync(int take, CancellationToken cancellationToken);
}

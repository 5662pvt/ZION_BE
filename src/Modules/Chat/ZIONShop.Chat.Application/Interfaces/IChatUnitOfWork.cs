namespace ZIONShop.Chat.Application.Interfaces;

public interface IChatUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Detaches a single tracked entity so a retry (after a unique-index or optimistic-
    /// concurrency conflict on <see cref="SaveChangesAsync"/>) can re-fetch fresh state for it instead of
    /// re-applying mutations on top of a now-stale in-memory instance, without disturbing other entities
    /// already tracked in the same unit of work (e.g. a <c>ChatMessage</c> still pending insert).</summary>
    void Detach(object entity);
}

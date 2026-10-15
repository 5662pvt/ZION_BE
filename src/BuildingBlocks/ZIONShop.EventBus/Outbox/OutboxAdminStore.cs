using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Admin;

namespace ZIONShop.EventBus.Outbox;

public class OutboxAdminStore<TDbContext> : IOutboxAdminStore where TDbContext : DbContext
{
    private readonly TDbContext _db;

    public OutboxAdminStore(TDbContext db) => _db = db;

    public string Module => ModuleNameResolver.FromDbContext(typeof(TDbContext));

    public Task<int> CountAsync(OutboxStatus? status, CancellationToken cancellationToken) =>
        Filter(_db.Set<OutboxMessage>().AsNoTracking(), status).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<OutboxMessageSummary>> ListNewestAsync(OutboxStatus? status, int take, CancellationToken cancellationToken)
    {
        var messages = await Filter(_db.Set<OutboxMessage>().AsNoTracking(), status)
            .OrderByDescending(m => m.OccurredOn)
            .Take(take)
            .ToListAsync(cancellationToken);

        return messages.Select(ToSummary).ToList();
    }

    public async Task<OutboxMessageDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await _db.Set<OutboxMessage>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (message is null) return null;

        return new OutboxMessageDetail(
            message.Id, Module, ShortEventTypeName(message.Type), message.OccurredOn, message.ProcessedOn,
            message.Attempts, StatusOf(message).ToString(), message.Error, message.TraceParent,
            PayloadMasker.MaskJson(message.Content));
    }

    public async Task<OutboxRetryOutcome> RetryAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await _db.Set<OutboxMessage>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (message is null) return OutboxRetryOutcome.NotFound;
        if (StatusOf(message) != OutboxStatus.Failed) return OutboxRetryOutcome.NotFailed;

        message.Attempts = 0;
        message.Error = null;
        await _db.SaveChangesAsync(cancellationToken);
        return OutboxRetryOutcome.Retried;
    }

    public async Task<int> RetryFailedAsync(CancellationToken cancellationToken)
    {
        var failed = await _db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null && m.Attempts >= OutboxPublisherService<TDbContext>.MaxAttempts)
            .ToListAsync(cancellationToken);

        foreach (var message in failed)
        {
            message.Attempts = 0;
            message.Error = null;
        }

        if (failed.Count > 0) await _db.SaveChangesAsync(cancellationToken);
        return failed.Count;
    }

    private static IQueryable<OutboxMessage> Filter(IQueryable<OutboxMessage> query, OutboxStatus? status) => status switch
    {
        OutboxStatus.Pending => query.Where(m => m.ProcessedOn == null && m.Attempts < OutboxPublisherService<TDbContext>.MaxAttempts),
        OutboxStatus.Failed => query.Where(m => m.ProcessedOn == null && m.Attempts >= OutboxPublisherService<TDbContext>.MaxAttempts),
        OutboxStatus.Processed => query.Where(m => m.ProcessedOn != null),
        _ => query
    };

    private static OutboxStatus StatusOf(OutboxMessage message)
    {
        if (message.ProcessedOn is not null) return OutboxStatus.Processed;
        return message.Attempts >= OutboxPublisherService<TDbContext>.MaxAttempts ? OutboxStatus.Failed : OutboxStatus.Pending;
    }

    private OutboxMessageSummary ToSummary(OutboxMessage message) => new(
        message.Id, Module, ShortEventTypeName(message.Type), message.OccurredOn, message.ProcessedOn,
        message.Attempts, StatusOf(message).ToString(), message.Error);

    /// <summary>"Namespace.ClassName, Assembly, ..." (as stored by <see cref="Outbox{TDbContext}"/> via
    /// <c>Type.AssemblyQualifiedName</c>) -> "ClassName", without loading the type via reflection.</summary>
    private static string ShortEventTypeName(string assemblyQualifiedName)
    {
        var typeName = assemblyQualifiedName.Split(',')[0];
        var lastDot = typeName.LastIndexOf('.');
        return lastDot >= 0 ? typeName[(lastDot + 1)..] : typeName;
    }
}

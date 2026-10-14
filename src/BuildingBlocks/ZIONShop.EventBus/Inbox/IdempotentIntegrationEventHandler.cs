using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.Inbox;

/// <summary>
/// Decorator giving at-least-once delivery exactly-once-processing semantics per (EventId, consumer),
/// recorded in the SAME <typeparamref name="TDbContext"/>/transaction as the inner handler's business changes.
/// The whole inner call -- including every <c>SaveChangesAsync</c> it makes on this same
/// <typeparamref name="TDbContext"/>, even across several calls (e.g. a handler that releases a coupon
/// then releases several gift redemptions in a loop, each with its own save) -- runs inside ONE explicit
/// database transaction owned by this decorator, committed only after the inner handler returns
/// successfully and the inbox row is persisted. If any later save in that sequence fails, the whole
/// transaction rolls back, including saves that already "succeeded" earlier in the same delivery, so a
/// partial effect can never be left behind while the inbox row still gets committed (which would make the
/// inbox skip the retry that was supposed to finish the job). A handler that already opens its own
/// explicit transaction on this same <typeparamref name="TDbContext"/> finds <c>Database.CurrentTransaction</c>
/// already set by this decorator and must join it via a SAVEPOINT instead of starting a second real
/// transaction (see <c>InventoryDbContext.BeginTransactionAsync</c>).
/// Concurrency: two concurrent deliveries of the same (EventId, consumer) both pass the existence check below,
/// both add an InboxMessage row, and both proceed to call the inner handler. The composite primary key
/// (MessageId, Consumer) makes whichever insert commits second violate a unique constraint inside the same
/// transaction that would also persist its business changes, so that whole transaction fails, the
/// exception propagates, and the message is redelivered. On redelivery the row already exists from the
/// winning attempt, so this handler is skipped. No Npgsql-specific code is needed -- any relational provider
/// enforcing the PK gives the same guarantee.
/// </summary>
public class IdempotentIntegrationEventHandler<TEvent, THandler, TDbContext> : IIntegrationEventHandler<TEvent>
    where TEvent : IntegrationEvent
    where THandler : class, IIntegrationEventHandler<TEvent>
    where TDbContext : DbContext
{
    private readonly THandler _inner;
    private readonly TDbContext _db;
    private readonly ILogger<IdempotentIntegrationEventHandler<TEvent, THandler, TDbContext>> _logger;

    public IdempotentIntegrationEventHandler(THandler inner, TDbContext db, ILogger<IdempotentIntegrationEventHandler<TEvent, THandler, TDbContext>> logger)
    {
        _inner = inner;
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        var consumer = typeof(THandler).FullName!;
        var eventType = typeof(TEvent).Name;

        var alreadyProcessed = await _db.Set<InboxMessage>()
            .AnyAsync(m => m.MessageId == @event.EventId && m.Consumer == consumer, cancellationToken);
        if (alreadyProcessed)
        {
            _logger.LogInformation("duplicate {EventType} {EventId} skipped by {Consumer}", eventType, @event.EventId, consumer);
            return;
        }

        var entry = _db.Set<InboxMessage>().Add(new InboxMessage
        {
            MessageId = @event.EventId,
            Consumer = consumer,
            EventType = eventType,
            ProcessedOn = DateTime.UtcNow
        });

        // Only begin a transaction if nothing has already opened one on this DbContext -- a handler
        // that calls BeginTransactionAsync itself (same scope/same TDbContext) must join this one via a
        // savepoint rather than race it, see the type doc comment above.
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            await _inner.HandleAsync(@event, cancellationToken);

            // Most handlers save through a uow bound to this same scoped TDbContext, which already
            // persisted the inbox row as part of one of its own SaveChangesAsync calls (all within the
            // transaction opened above, so still uncommitted). A handler that opens its own inner scopes
            // (fresh DbContext instances) never touches this entry, so it is still Added here.
            if (entry.State == EntityState.Added)
                await _db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Nothing recorded, nothing committed -- the message is redelivered and retried in full.
            if (transaction is not null)
                await transaction.RollbackAsync(cancellationToken);
            entry.State = EntityState.Detached;
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }
}

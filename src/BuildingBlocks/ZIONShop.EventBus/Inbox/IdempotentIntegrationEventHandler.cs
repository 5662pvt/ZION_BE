using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.Inbox;

/// <summary>
/// Decorator giving at-least-once delivery exactly-once-processing semantics per (EventId, consumer),
/// recorded in the SAME <typeparamref name="TDbContext"/>/transaction as the inner handler's business changes.
/// Concurrency: two concurrent deliveries of the same (EventId, consumer) both pass the existence check below,
/// both add an InboxMessage row, and both proceed to call the inner handler. The composite primary key
/// (MessageId, Consumer) makes whichever insert commits second violate a unique constraint inside the same
/// SaveChangesAsync call that would also persist its business changes, so that whole transaction fails, the
/// exception propagates, and the message is redelivered. On redelivery the row already exists from the
/// winning attempt, so this handler is skipped. No Npgsql-specific code is needed — any relational provider
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

        try
        {
            await _inner.HandleAsync(@event, cancellationToken);
        }
        catch
        {
            // Nothing recorded, nothing committed -- the message is redelivered and retried in full.
            entry.State = EntityState.Detached;
            throw;
        }

        // Most handlers save through a uow bound to this same scoped TDbContext, which persists the
        // inbox row atomically with the business change. A handler that opens its own inner scopes
        // (fresh DbContext instances) never touches this entry, so it is still Added here -- save it
        // now so the row still exists for the next redelivery, even though it then lands after the
        // handler's own (separately committed) effect rather than atomically with it.
        if (entry.State == EntityState.Added)
            await _db.SaveChangesAsync(cancellationToken);
    }
}

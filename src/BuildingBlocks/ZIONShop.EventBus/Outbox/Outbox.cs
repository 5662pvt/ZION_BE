using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.Outbox;

public class Outbox<TDbContext> : IOutbox<TDbContext> where TDbContext : DbContext
{
    private readonly TDbContext _db;

    public Outbox(TDbContext db) => _db = db;

    public void Add(IntegrationEvent @event)
    {
        var eventType = @event.GetType();
        var message = new OutboxMessage
        {
            Id = @event.EventId,
            Type = eventType.AssemblyQualifiedName ?? throw new InvalidOperationException($"Event type '{eventType}' has no assembly-qualified name."),
            Content = JsonSerializer.Serialize(@event, eventType),
            OccurredOn = @event.OccurredOn,
            Attempts = 0
        };
        _db.Set<OutboxMessage>().Add(message);
    }
}

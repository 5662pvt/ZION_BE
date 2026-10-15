using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Admin;

namespace ZIONShop.EventBus.Inbox;

public class InboxAdminStore<TDbContext> : IInboxAdminStore where TDbContext : DbContext
{
    private readonly TDbContext _db;

    public InboxAdminStore(TDbContext db) => _db = db;

    public string Module => ModuleNameResolver.FromDbContext(typeof(TDbContext));

    public Task<int> CountAsync(CancellationToken cancellationToken) =>
        _db.Set<InboxMessage>().AsNoTracking().CountAsync(cancellationToken);

    public async Task<IReadOnlyList<InboxMessageSummary>> ListNewestAsync(int take, CancellationToken cancellationToken)
    {
        var messages = await _db.Set<InboxMessage>().AsNoTracking()
            .OrderByDescending(m => m.ProcessedOn)
            .Take(take)
            .ToListAsync(cancellationToken);

        return messages
            .Select(m => new InboxMessageSummary(m.MessageId, Module, m.Consumer, m.EventType, m.ProcessedOn))
            .ToList();
    }
}

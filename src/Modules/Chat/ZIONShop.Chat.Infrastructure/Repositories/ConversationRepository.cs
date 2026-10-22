using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Chat.Infrastructure.Persistence;

namespace ZIONShop.Chat.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly ChatDbContext _db;

    public ConversationRepository(ChatDbContext db) => _db = db;

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Conversation?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        _db.Conversations.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) =>
        await _db.Conversations.AddAsync(conversation, cancellationToken);

    public IQueryable<Conversation> Query() => _db.Conversations.AsNoTracking();
}

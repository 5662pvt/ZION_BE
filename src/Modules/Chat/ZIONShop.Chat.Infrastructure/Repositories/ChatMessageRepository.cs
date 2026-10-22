using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Chat.Infrastructure.Persistence;

namespace ZIONShop.Chat.Infrastructure.Repositories;

public class ChatMessageRepository : IChatMessageRepository
{
    private readonly ChatDbContext _db;

    public ChatMessageRepository(ChatDbContext db) => _db = db;

    public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default) =>
        await _db.Messages.AddAsync(message, cancellationToken);

    public IQueryable<ChatMessage> Query() => _db.Messages.AsNoTracking();
}

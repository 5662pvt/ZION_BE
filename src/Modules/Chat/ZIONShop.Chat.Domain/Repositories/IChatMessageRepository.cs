using ZIONShop.Chat.Domain.Entities;

namespace ZIONShop.Chat.Domain.Repositories;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);

    IQueryable<ChatMessage> Query();
}

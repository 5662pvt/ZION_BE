using ZIONShop.Chat.Domain.Entities;

namespace ZIONShop.Chat.Domain.Repositories;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Conversation?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    IQueryable<Conversation> Query();
}

using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Contracts.Auth;

namespace ZIONShop.Chat.Application.Services;

/// <summary>Single customer conversation, created lazily on first GET/send.</summary>
public class ConversationProvisioningService
{
    private readonly IConversationRepository _conversations;
    private readonly IUserContactReader _contacts;
    private readonly IChatUnitOfWork _uow;
    private readonly TimeProvider _time;

    public ConversationProvisioningService(IConversationRepository conversations, IUserContactReader contacts, IChatUnitOfWork uow, TimeProvider time)
    {
        _conversations = conversations;
        _contacts = contacts;
        _uow = uow;
        _time = time;
    }

    public async Task<Conversation> GetOrCreateAsync(Guid customerId, CancellationToken ct)
    {
        var existing = await _conversations.GetByCustomerIdAsync(customerId, ct);
        if (existing is not null) return existing;

        var contact = await _contacts.GetAsync(customerId, ct);
        var name = contact?.DisplayName ?? contact?.Email ?? "Khách hàng";
        var email = contact?.Email ?? string.Empty;

        var conversation = Conversation.Create(customerId, name, email, _time.GetUtcNow().UtcDateTime);
        await _conversations.AddAsync(conversation, ct);
        await _uow.SaveChangesAsync(ct);
        return conversation;
    }
}

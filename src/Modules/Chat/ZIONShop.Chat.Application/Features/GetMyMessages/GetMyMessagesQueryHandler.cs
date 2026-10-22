using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetMyMessages;

public class GetMyMessagesQueryHandler : IRequestHandler<GetMyMessagesQuery, Result<ChatMessagePageDto>>
{
    private const int DefaultLimit = 30;
    private const int MaxLimit = 100;

    private readonly IConversationRepository _conversations;
    private readonly IChatMessageRepository _messages;

    public GetMyMessagesQueryHandler(IConversationRepository conversations, IChatMessageRepository messages)
    {
        _conversations = conversations;
        _messages = messages;
    }

    public async Task<Result<ChatMessagePageDto>> Handle(GetMyMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByCustomerIdAsync(request.UserId, cancellationToken);
        if (conversation is null)
            return Result.Success(new ChatMessagePageDto(Array.Empty<ChatMessageDto>(), false));

        var limit = request.Limit is < 1 or > MaxLimit ? DefaultLimit : request.Limit;

        var query = _messages.Query().Where(m => m.ConversationId == conversation.Id);
        if (request.Before.HasValue) query = query.Where(m => m.CreatedAt < request.Before.Value);
        query = query.OrderByDescending(m => m.CreatedAt);

        var page = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = page.Count > limit;
        var items = page.Take(limit).Select(m => m.ToDto(conversation.CustomerName)).ToList();

        return Result.Success(new ChatMessagePageDto(items, hasMore));
    }
}

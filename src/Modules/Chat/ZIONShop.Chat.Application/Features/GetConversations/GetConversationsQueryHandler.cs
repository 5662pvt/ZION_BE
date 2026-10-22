using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversations;

public class GetConversationsQueryHandler : IRequestHandler<GetConversationsQuery, Result<PagedResult<ConversationDto>>>
{
    private readonly IConversationRepository _conversations;

    public GetConversationsQueryHandler(IConversationRepository conversations) => _conversations = conversations;

    public async Task<Result<PagedResult<ConversationDto>>> Handle(GetConversationsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Conversation> query = _conversations.Query();

        if (request.UnreadOnly)
            query = query.Where(c => c.StaffUnread > 0);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var kw = request.Search.Trim();
            query = query.Where(c => EF.Functions.Like(c.CustomerName, $"%{kw}%") || EF.Functions.Like(c.CustomerEmail, $"%{kw}%"));
        }

        query = query.OrderByDescending(c => c.LastMessageAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Result.Success(PagedResult<ConversationDto>.Create(items.Select(c => c.ToDto()).ToList(), page, pageSize, total));
    }
}

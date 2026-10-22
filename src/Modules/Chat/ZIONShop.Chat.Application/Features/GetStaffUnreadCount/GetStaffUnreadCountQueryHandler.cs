using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetStaffUnreadCount;

public class GetStaffUnreadCountQueryHandler : IRequestHandler<GetStaffUnreadCountQuery, Result<StaffUnreadCountDto>>
{
    private readonly IConversationRepository _conversations;

    public GetStaffUnreadCountQueryHandler(IConversationRepository conversations) => _conversations = conversations;

    public async Task<Result<StaffUnreadCountDto>> Handle(GetStaffUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var unread = await _conversations.Query()
            .Where(c => c.StaffUnread > 0)
            .Select(c => c.StaffUnread)
            .ToListAsync(cancellationToken);

        return Result.Success(new StaffUnreadCountDto(unread.Count, unread.Sum()));
    }
}

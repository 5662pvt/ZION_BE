using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Realtime;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.MarkConversationReadByStaff;

public class MarkConversationReadByStaffCommandHandler : IRequestHandler<MarkConversationReadByStaffCommand, Result<ConversationDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IChatUnitOfWork _uow;
    private readonly IRealtimeEventPublisher _realtime;
    private readonly TimeProvider _time;

    public MarkConversationReadByStaffCommandHandler(IConversationRepository conversations, IChatUnitOfWork uow, IRealtimeEventPublisher realtime, TimeProvider time)
    {
        _conversations = conversations;
        _uow = uow;
        _realtime = realtime;
        _time = time;
    }

    public async Task<Result<ConversationDto>> Handle(MarkConversationReadByStaffCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null) return Result.Failure<ConversationDto>(ChatErrors.ConversationNotFound);

        conversation.MarkReadBy(ChatSenderRole.Staff, _time.GetUtcNow().UtcDateTime);
        await _uow.SaveChangesAsync(cancellationToken);

        var dto = conversation.ToDto();

        try
        {
            await _realtime.PublishToUserAsync(conversation.CustomerId, "chat:read", new { conversationId = conversation.Id, side = "Staff", readAt = conversation.StaffLastReadAt }, cancellationToken);
        }
        catch
        {
            // fail-open: realtime push must never fail the triggering business transaction
        }

        return Result.Success(dto);
    }
}

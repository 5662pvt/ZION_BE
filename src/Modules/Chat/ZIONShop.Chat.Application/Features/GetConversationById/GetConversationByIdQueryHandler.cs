using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversationById;

public class GetConversationByIdQueryHandler : IRequestHandler<GetConversationByIdQuery, Result<ConversationDto>>
{
    private readonly IConversationRepository _conversations;

    public GetConversationByIdQueryHandler(IConversationRepository conversations) => _conversations = conversations;

    public async Task<Result<ConversationDto>> Handle(GetConversationByIdQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null) return Result.Failure<ConversationDto>(ChatErrors.ConversationNotFound);
        return Result.Success(conversation.ToDto());
    }
}

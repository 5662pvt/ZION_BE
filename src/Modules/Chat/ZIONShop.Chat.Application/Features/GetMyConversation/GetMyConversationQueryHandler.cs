using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Application.Services;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetMyConversation;

public class GetMyConversationQueryHandler : IRequestHandler<GetMyConversationQuery, Result<ConversationDto>>
{
    private readonly ConversationProvisioningService _provisioning;

    public GetMyConversationQueryHandler(ConversationProvisioningService provisioning) => _provisioning = provisioning;

    public async Task<Result<ConversationDto>> Handle(GetMyConversationQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _provisioning.GetOrCreateAsync(request.UserId, cancellationToken);
        return Result.Success(conversation.ToDto());
    }
}

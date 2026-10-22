using MediatR;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversationIdForCustomer;

public class GetConversationIdForCustomerQueryHandler : IRequestHandler<GetConversationIdForCustomerQuery, Result<Guid?>>
{
    private readonly IConversationRepository _conversations;

    public GetConversationIdForCustomerQueryHandler(IConversationRepository conversations) => _conversations = conversations;

    public async Task<Result<Guid?>> Handle(GetConversationIdForCustomerQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByCustomerIdAsync(request.UserId, cancellationToken);
        return Result.Success<Guid?>(conversation?.Id);
    }
}

using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversationIdForCustomer;

/// <summary>Non-creating lookup for the hub's <c>Typing</c> relay -- unlike <c>GetMyConversationQuery</c>,
/// does not lazily create a conversation (nothing to relay typing for if one doesn't exist yet).</summary>
public record GetConversationIdForCustomerQuery(Guid UserId) : IRequest<Result<Guid?>>;

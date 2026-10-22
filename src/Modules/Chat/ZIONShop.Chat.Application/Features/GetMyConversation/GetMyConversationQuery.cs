using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetMyConversation;

public record GetMyConversationQuery(Guid UserId) : IRequest<Result<ConversationDto>>;

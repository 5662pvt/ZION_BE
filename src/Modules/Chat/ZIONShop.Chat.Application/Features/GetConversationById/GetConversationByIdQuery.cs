using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversationById;

public record GetConversationByIdQuery(Guid ConversationId) : IRequest<Result<ConversationDto>>;

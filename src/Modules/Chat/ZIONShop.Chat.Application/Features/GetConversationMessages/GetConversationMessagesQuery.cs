using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetConversationMessages;

public record GetConversationMessagesQuery(Guid ConversationId, DateTime? Before, int Limit) : IRequest<Result<ChatMessagePageDto>>;

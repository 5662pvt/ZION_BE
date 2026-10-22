using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.MarkMyConversationRead;

public record MarkMyConversationReadCommand(Guid UserId) : IRequest<Result<ConversationDto>>;

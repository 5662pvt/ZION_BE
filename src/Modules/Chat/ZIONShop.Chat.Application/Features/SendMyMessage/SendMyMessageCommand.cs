using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendMyMessage;

public record SendMyMessageCommand(Guid UserId, string Type, string? Text, Guid? ProductId, Guid? OrderId) : IRequest<Result<ChatMessageDto>>;

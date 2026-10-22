using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendStaffMessage;

public record SendStaffMessageCommand(Guid StaffId, Guid ConversationId, string Type, string? Text, Guid? ProductId, Guid? OrderId) : IRequest<Result<ChatMessageDto>>;

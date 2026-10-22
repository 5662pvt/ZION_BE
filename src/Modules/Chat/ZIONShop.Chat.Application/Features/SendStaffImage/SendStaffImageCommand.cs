using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendStaffImage;

public record SendStaffImageCommand(Guid StaffId, Guid ConversationId, Stream Content, string FileName, string ContentType, long Length, string? Caption) : IRequest<Result<ChatMessageDto>>;

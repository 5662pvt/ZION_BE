using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.MarkConversationReadByStaff;

public record MarkConversationReadByStaffCommand(Guid ConversationId) : IRequest<Result<ConversationDto>>;

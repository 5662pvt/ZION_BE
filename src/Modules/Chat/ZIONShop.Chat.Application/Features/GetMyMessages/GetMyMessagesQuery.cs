using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.GetMyMessages;

public record GetMyMessagesQuery(Guid UserId, DateTime? Before, int Limit) : IRequest<Result<ChatMessagePageDto>>;

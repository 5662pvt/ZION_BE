using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendMyImage;

public record SendMyImageCommand(Guid UserId, Stream Content, string FileName, string ContentType, long Length, string? Caption) : IRequest<Result<ChatMessageDto>>;

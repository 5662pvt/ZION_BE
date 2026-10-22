using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Contracts.Media;
using ZIONShop.Realtime;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendStaffImage;

public class SendStaffImageCommandHandler : IRequestHandler<SendStaffImageCommand, Result<ChatMessageDto>>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private const string Folder = "chat";
    private const int MaxSaveAttempts = 3;

    private readonly IConversationRepository _conversations;
    private readonly IChatMessageRepository _messages;
    private readonly IChatUnitOfWork _uow;
    private readonly IMediaStorage _media;
    private readonly IRealtimeEventPublisher _realtime;
    private readonly TimeProvider _time;

    public SendStaffImageCommandHandler(
        IConversationRepository conversations,
        IChatMessageRepository messages,
        IChatUnitOfWork uow,
        IMediaStorage media,
        IRealtimeEventPublisher realtime,
        TimeProvider time)
    {
        _conversations = conversations;
        _messages = messages;
        _uow = uow;
        _media = media;
        _realtime = realtime;
        _time = time;
    }

    public async Task<Result<ChatMessageDto>> Handle(SendStaffImageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null) return Result.Failure<ChatMessageDto>(ChatErrors.ConversationNotFound);

        if (!AllowedContentTypes.Contains(request.ContentType))
            return Result.Failure<ChatMessageDto>(ChatErrors.InvalidImage);
        if (request.Length is <= 0 or > MaxImageBytes)
            return Result.Failure<ChatMessageDto>(ChatErrors.ImageTooLarge);

        var caption = request.Caption?.Trim();
        if (caption is { Length: > 500 })
            return Result.Failure<ChatMessageDto>(ChatErrors.TooLong);

        var (media, error) = await _media.UploadImageAsync(request.Content, request.FileName, request.ContentType, Folder, cancellationToken);
        if (media is null)
            return Result.Failure<ChatMessageDto>(ChatErrors.InvalidImage);

        var now = _time.GetUtcNow().UtcDateTime;
        var previewText = ChatMessage.BuildPreview(ChatMessageType.Image, caption);

        var message = conversation.AddMessage(
            request.StaffId, ChatSenderRole.Staff, ChatMessageType.Image, caption,
            imageUrl: media.Url, imagePublicId: media.PublicId,
            productId: null, orderId: null, cardJson: null, previewText, now);

        await _messages.AddAsync(message, cancellationToken);

        for (var attempt = 1; attempt <= MaxSaveAttempts; attempt++)
        {
            try
            {
                await _uow.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                // Another concurrent send/mark-read on this conversation beat us to it -- re-fetch fresh
                // state and re-apply only the counter/preview update; the ChatMessage row stays as-is.
                _uow.Detach(conversation);
                conversation = await _conversations.GetByIdAsync(request.ConversationId, cancellationToken)
                    ?? throw new InvalidOperationException("Conversation disappeared during a concurrent retry.");
                conversation.ApplyMessageSummary(ChatSenderRole.Staff, previewText, now);
            }
        }

        var dto = message.ToDto(conversation.CustomerName);

        try
        {
            var conversationDto = conversation.ToDto();
            await _realtime.PublishToUserAsync(conversation.CustomerId, "chat:message", new { conversationId = conversation.Id, message = dto, conversation = conversationDto }, cancellationToken);
            await _realtime.PublishToStaffAsync("chat:message", new { conversationId = conversation.Id, message = dto, conversation = conversationDto }, cancellationToken);
        }
        catch
        {
            // fail-open: realtime push must never fail the triggering business transaction
        }

        return Result.Success(dto);
    }
}

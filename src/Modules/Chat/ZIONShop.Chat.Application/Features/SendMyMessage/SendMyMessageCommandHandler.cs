using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Realtime;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendMyMessage;

public class SendMyMessageCommandHandler : IRequestHandler<SendMyMessageCommand, Result<ChatMessageDto>>
{
    private const int MaxSaveAttempts = 3;

    private readonly ConversationProvisioningService _provisioning;
    private readonly IChatMessageRepository _messages;
    private readonly IChatUnitOfWork _uow;
    private readonly ChatCardService _cards;
    private readonly IRealtimeEventPublisher _realtime;
    private readonly TimeProvider _time;

    public SendMyMessageCommandHandler(
        ConversationProvisioningService provisioning,
        IChatMessageRepository messages,
        IChatUnitOfWork uow,
        ChatCardService cards,
        IRealtimeEventPublisher realtime,
        TimeProvider time)
    {
        _provisioning = provisioning;
        _messages = messages;
        _uow = uow;
        _cards = cards;
        _realtime = realtime;
        _time = time;
    }

    public async Task<Result<ChatMessageDto>> Handle(SendMyMessageCommand request, CancellationToken cancellationToken)
    {
        var type = Enum.Parse<ChatMessageType>(request.Type);
        var text = request.Text?.Trim();

        if (type == ChatMessageType.Text && string.IsNullOrEmpty(text))
            return Result.Failure<ChatMessageDto>(ChatErrors.EmptyMessage);
        if (text is not null && text.Length > 2000)
            return Result.Failure<ChatMessageDto>(ChatErrors.TooLong);

        string? cardJson = null;
        string previewProductName = string.Empty;
        string previewOrderCode = string.Empty;

        if (type == ChatMessageType.Product)
        {
            var resolved = await _cards.ResolveProductAsync(request.ProductId!.Value, requirePublished: true, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<ChatMessageDto>(resolved.Error);
            cardJson = resolved.Value.Card.ToCardJson();
            previewProductName = resolved.Value.ProductName;
        }
        else if (type == ChatMessageType.Order)
        {
            var resolved = await _cards.ResolveOrderAsync(request.OrderId!.Value, request.UserId, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<ChatMessageDto>(resolved.Error);
            cardJson = resolved.Value.Card.ToCardJson();
            previewOrderCode = resolved.Value.OrderCode;
        }

        var conversation = await _provisioning.GetOrCreateAsync(request.UserId, cancellationToken);
        var now = _time.GetUtcNow().UtcDateTime;
        var previewText = Domain.Entities.ChatMessage.BuildPreview(type, text, previewProductName, previewOrderCode);

        var message = conversation.AddMessage(
            request.UserId, ChatSenderRole.Customer, type, text,
            imageUrl: null, imagePublicId: null,
            request.ProductId, request.OrderId, cardJson, previewText, now);

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
                conversation = await _provisioning.GetOrCreateAsync(request.UserId, cancellationToken);
                conversation.ApplyMessageSummary(ChatSenderRole.Customer, previewText, now);
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

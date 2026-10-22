using MediatR;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Mappings;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Realtime;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Application.Features.SendStaffMessage;

public class SendStaffMessageCommandHandler : IRequestHandler<SendStaffMessageCommand, Result<ChatMessageDto>>
{
    private readonly IConversationRepository _conversations;
    private readonly IChatMessageRepository _messages;
    private readonly IChatUnitOfWork _uow;
    private readonly ChatCardService _cards;
    private readonly IRealtimeEventPublisher _realtime;
    private readonly TimeProvider _time;

    public SendStaffMessageCommandHandler(
        IConversationRepository conversations,
        IChatMessageRepository messages,
        IChatUnitOfWork uow,
        ChatCardService cards,
        IRealtimeEventPublisher realtime,
        TimeProvider time)
    {
        _conversations = conversations;
        _messages = messages;
        _uow = uow;
        _cards = cards;
        _realtime = realtime;
        _time = time;
    }

    public async Task<Result<ChatMessageDto>> Handle(SendStaffMessageCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdAsync(request.ConversationId, cancellationToken);
        if (conversation is null) return Result.Failure<ChatMessageDto>(ChatErrors.ConversationNotFound);

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
            var resolved = await _cards.ResolveProductAsync(request.ProductId!.Value, requirePublished: false, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<ChatMessageDto>(resolved.Error);
            cardJson = resolved.Value.Card.ToCardJson();
            previewProductName = resolved.Value.ProductName;
        }
        else if (type == ChatMessageType.Order)
        {
            var resolved = await _cards.ResolveOrderAsync(request.OrderId!.Value, conversation.CustomerId, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<ChatMessageDto>(resolved.Error);
            cardJson = resolved.Value.Card.ToCardJson();
            previewOrderCode = resolved.Value.OrderCode;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var previewText = Domain.Entities.ChatMessage.BuildPreview(type, text, previewProductName, previewOrderCode);

        var message = conversation.AddMessage(
            request.StaffId, ChatSenderRole.Staff, type, text,
            imageUrl: null, imagePublicId: null,
            request.ProductId, request.OrderId, cardJson, previewText, now);

        await _messages.AddAsync(message, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

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

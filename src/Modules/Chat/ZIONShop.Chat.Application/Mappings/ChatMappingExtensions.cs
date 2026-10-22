using System.Text.Json;
using ZIONShop.Chat.Application.DTOs;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Enums;

namespace ZIONShop.Chat.Application.Mappings;

public static class ChatMappingExtensions
{
    public const string ZionShopSenderName = "ZIONShop";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ToCardJson(this ChatProductCardDto card) => JsonSerializer.Serialize(card, JsonOptions);
    public static string ToCardJson(this ChatOrderCardDto card) => JsonSerializer.Serialize(card, JsonOptions);

    public static ConversationDto ToDto(this Conversation conversation) => new(
        conversation.Id,
        conversation.CustomerId,
        conversation.CustomerName,
        conversation.CustomerEmail,
        conversation.LastMessageAt,
        conversation.LastMessagePreview,
        conversation.LastMessageSender.ToString(),
        conversation.CustomerUnread,
        conversation.StaffUnread,
        conversation.CustomerLastReadAt,
        conversation.StaffLastReadAt);

    /// <summary>Hydrates Product/Order cards from the JSON snapshot stored at send time -- no call back
    /// to Contracts readers is needed to list historical messages.</summary>
    public static ChatMessageDto ToDto(this ChatMessage message, string customerName)
    {
        ChatProductCardDto? product = null;
        ChatOrderCardDto? order = null;

        if (message.CardJson is not null)
        {
            if (message.Type == ChatMessageType.Product)
                product = JsonSerializer.Deserialize<ChatProductCardDto>(message.CardJson, JsonOptions);
            else if (message.Type == ChatMessageType.Order)
                order = JsonSerializer.Deserialize<ChatOrderCardDto>(message.CardJson, JsonOptions);
        }

        return new ChatMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.SenderRole.ToString(),
            message.SenderRole == ChatSenderRole.Customer ? customerName : ZionShopSenderName,
            message.Type.ToString(),
            message.Text,
            message.ImageUrl,
            product,
            order,
            message.CreatedAt);
    }
}

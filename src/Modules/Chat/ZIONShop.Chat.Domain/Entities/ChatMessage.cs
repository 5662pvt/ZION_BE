using ZIONShop.Chat.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Chat.Domain.Entities;

/// <summary>Append-only message row. Not a child collection of <see cref="Conversation"/> -- queried
/// directly by <c>ConversationId</c>, same split as Loyalty's PointAccount/PointTransaction.</summary>
public class ChatMessage : BaseEntity
{
    private const int PreviewMaxLength = 120;

    private ChatMessage() { }

    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public ChatSenderRole SenderRole { get; private set; }
    public ChatMessageType Type { get; private set; }
    public string? Text { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? ImagePublicId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? OrderId { get; private set; }
    public string? CardJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static ChatMessage Create(
        Guid conversationId,
        Guid senderId,
        ChatSenderRole senderRole,
        ChatMessageType type,
        string? text,
        string? imageUrl,
        string? imagePublicId,
        Guid? productId,
        Guid? orderId,
        string? cardJson,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        ConversationId = conversationId,
        SenderId = senderId,
        SenderRole = senderRole,
        Type = type,
        Text = text,
        ImageUrl = imageUrl,
        ImagePublicId = imagePublicId,
        ProductId = productId,
        OrderId = orderId,
        CardJson = cardJson,
        CreatedAt = now
    };

    /// <summary>Preview text shown on the conversation list, per message type.</summary>
    public static string BuildPreview(ChatMessageType type, string? text, string? productName = null, string? orderCode = null)
    {
        var preview = type switch
        {
            ChatMessageType.Text => text ?? string.Empty,
            ChatMessageType.Image => "[Hình ảnh]",
            ChatMessageType.Product => $"[Sản phẩm] {productName}",
            ChatMessageType.Order => $"[Đơn hàng] #{orderCode}",
            _ => string.Empty
        };

        return preview.Length <= PreviewMaxLength ? preview : preview[..PreviewMaxLength];
    }
}

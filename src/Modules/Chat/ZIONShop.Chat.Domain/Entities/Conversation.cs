using ZIONShop.Chat.Domain.Enums;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Chat.Domain.Entities;

/// <summary>One conversation per customer (single shop, shared staff inbox -- no assignment).</summary>
public class Conversation : BaseEntity
{
    private Conversation() { }

    public Guid CustomerId { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerEmail { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public string LastMessagePreview { get; private set; } = string.Empty;
    public ChatSenderRole LastMessageSender { get; private set; }
    public int CustomerUnread { get; private set; }
    public int StaffUnread { get; private set; }
    public DateTime? CustomerLastReadAt { get; private set; }
    public DateTime? StaffLastReadAt { get; private set; }

    public static Conversation Create(Guid customerId, string customerName, string customerEmail, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        CustomerName = customerName,
        CustomerEmail = customerEmail,
        CreatedAt = now,
        LastMessageAt = now,
        LastMessagePreview = string.Empty,
        LastMessageSender = ChatSenderRole.Customer,
        CustomerUnread = 0,
        StaffUnread = 0,
        CustomerLastReadAt = null,
        StaffLastReadAt = null
    };

    /// <summary>Creates the message and updates the denormalized conversation summary/unread counters.
    /// <paramref name="previewText"/> is computed by the caller via <see cref="ChatMessage.BuildPreview"/>
    /// since it may need a product/order name the domain doesn't hold.</summary>
    public ChatMessage AddMessage(
        Guid senderId,
        ChatSenderRole senderRole,
        ChatMessageType type,
        string? text,
        string? imageUrl,
        string? imagePublicId,
        Guid? productId,
        Guid? orderId,
        string? cardJson,
        string previewText,
        DateTime now)
    {
        var message = ChatMessage.Create(Id, senderId, senderRole, type, text, imageUrl, imagePublicId, productId, orderId, cardJson, now);

        LastMessageAt = now;
        LastMessagePreview = previewText;
        LastMessageSender = senderRole;

        if (senderRole == ChatSenderRole.Customer) StaffUnread++;
        else CustomerUnread++;

        return message;
    }

    public void MarkReadBy(ChatSenderRole side, DateTime now)
    {
        if (side == ChatSenderRole.Customer)
        {
            CustomerUnread = 0;
            CustomerLastReadAt = now;
        }
        else
        {
            StaffUnread = 0;
            StaffLastReadAt = now;
        }
    }
}

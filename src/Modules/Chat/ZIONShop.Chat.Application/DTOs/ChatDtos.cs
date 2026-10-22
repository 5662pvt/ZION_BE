namespace ZIONShop.Chat.Application.DTOs;

public record ConversationDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTime LastMessageAt,
    string LastMessagePreview,
    string LastMessageSender,
    int CustomerUnread,
    int StaffUnread,
    DateTime? CustomerLastReadAt,
    DateTime? StaffLastReadAt);

public record ChatProductCardDto(Guid Id, string Name, string Slug, decimal MinPrice, decimal MaxPrice, string Currency, string? ImageUrl);

public record ChatOrderCardDto(Guid Id, string Code, string Status, decimal Total, string Currency, string FirstItemName, int ItemCount);

public record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderRole,
    string SenderName,
    string Type,
    string? Text,
    string? ImageUrl,
    ChatProductCardDto? Product,
    ChatOrderCardDto? Order,
    DateTime CreatedAt,
    string? StaffEmail = null);

public record ChatMessagePageDto(IReadOnlyList<ChatMessageDto> Items, bool HasMore);

public record StaffUnreadCountDto(int Conversations, int Messages);

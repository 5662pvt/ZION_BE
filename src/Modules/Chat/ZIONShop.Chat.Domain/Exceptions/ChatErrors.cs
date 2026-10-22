using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Chat.Domain.Exceptions;

public static class ChatErrors
{
    public static readonly Error EmptyMessage = Error.Validation("Chat.EmptyMessage", "Vui lòng nhập nội dung tin nhắn");
    public static readonly Error TooLong = Error.Validation("Chat.TooLong", "Nội dung tin nhắn quá dài");
    public static readonly Error InvalidImage = Error.Validation("Chat.InvalidImage", "Ảnh không hợp lệ, chỉ hỗ trợ JPG, PNG hoặc WEBP");
    public static readonly Error ImageTooLarge = Error.Validation("Chat.ImageTooLarge", "Ảnh vượt quá kích thước cho phép (tối đa 5MB)");
    public static readonly Error ProductNotFound = Error.NotFound("Chat.ProductNotFound", "Không tìm thấy sản phẩm");
    public static readonly Error OrderNotFound = Error.NotFound("Chat.OrderNotFound", "Không tìm thấy đơn hàng");
    public static readonly Error ConversationNotFound = Error.NotFound("Chat.ConversationNotFound", "Không tìm thấy cuộc trò chuyện");
}

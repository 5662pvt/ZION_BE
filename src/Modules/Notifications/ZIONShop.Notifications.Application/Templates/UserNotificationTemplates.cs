namespace ZIONShop.Notifications.Application.Templates;

/// <summary>Short Vietnamese in-app notification text -- same tone as <see cref="OrderEmailTemplates"/>, much shorter (a push, not an email).</summary>
public static class UserNotificationTemplates
{
    public static (string Title, string Message) OrderPlaced(string orderCode) =>
        ("Đặt hàng thành công", $"Đơn hàng #{orderCode} đã được đặt thành công.");

    public static (string Title, string Message) OrderConfirmed(string orderCode) =>
        ("Đơn hàng đã được xác nhận", $"Đơn hàng #{orderCode} đã được xác nhận và đang được chuẩn bị.");

    public static (string Title, string Message) OrderShipped(string orderCode) =>
        ("Đơn hàng đang được giao", $"Đơn hàng #{orderCode} đang được giao đến bạn.");

    public static (string Title, string Message) OrderCancelled(string orderCode) =>
        ("Đơn hàng đã bị huỷ", $"Đơn hàng #{orderCode} đã bị huỷ.");

    public static (string Title, string Message) PaymentCompleted(string orderCode) =>
        ("Thanh toán thành công", $"Thanh toán cho đơn hàng #{orderCode} đã thành công.");

    public static (string Title, string Message) PaymentFailed(string orderCode) =>
        ("Thanh toán không thành công", $"Thanh toán cho đơn hàng #{orderCode} không thành công.");

    public static (string Title, string Message) OrderCreatedForReview(string orderCode) =>
        ("Đơn hàng mới cần xử lý", $"Đơn hàng #{orderCode} vừa được đặt, cần kiểm tra và xử lý.");

    public static (string Title, string Message) OrderCompleted(string orderCode) =>
        ("Đơn hàng đã hoàn tất", $"Đơn hàng #{orderCode} đã được hoàn tất.");
}

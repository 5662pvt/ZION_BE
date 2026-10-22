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

    public static (string Title, string Message) ReturnRequested(string orderCode, string reason) =>
        ("Yêu cầu trả hàng mới", $"Đơn #{orderCode} có yêu cầu trả hàng ({reason}), cần xem xét.");

    public static (string Title, string Message) ReturnApproved(string orderCode) =>
        ("Yêu cầu trả hàng đã được chấp nhận", $"Vui lòng gửi hàng về cho shop và bấm 'Đã gửi hàng' trong đơn #{orderCode}.");

    public static (string Title, string Message) ReturnRejected(string orderCode, string rejectReason) =>
        ("Yêu cầu trả hàng bị từ chối", $"Yêu cầu trả hàng cho đơn #{orderCode} đã bị từ chối. Lý do: {rejectReason}.");

    public static (string Title, string Message) ReturnShippedBack(string orderCode, string? trackingCode) =>
        ("Khách đã gửi hàng trả", $"Đơn #{orderCode}: khách đã gửi hàng về (mã vận đơn: {trackingCode ?? "không có"}). Hãy xác nhận khi nhận được.");

    public static (string Title, string Message) ReturnRefunded(string orderCode, decimal amount, string method) =>
        ("Đã hoàn tiền", $"Đơn #{orderCode} đã được hoàn {amount:N0}đ ({(string.Equals(method, "Online", StringComparison.OrdinalIgnoreCase) ? "về phương thức thanh toán" : "tiền mặt")}).");

    public static (string Title, string Message) PointsEarnedFromOrder(int points, string orderCode) =>
        ("Bạn nhận được Xu", $"Bạn nhận được {points} Xu từ đơn hàng #{orderCode}.");

    public static (string Title, string Message) PointsEarnedFromReview(int points) =>
        ("Bạn nhận được Xu", $"Bạn nhận được {points} Xu nhờ đánh giá sản phẩm.");

    public static (string Title, string Message) PointsEarnedFromAdminAdjust(int points) =>
        ("Bạn được cộng Xu", $"Bạn được cộng {points} Xu.");

    public static (string Title, string Message) PointsReturned(int points, string orderCode, string reason) =>
        ("Đã hoàn Xu", reason switch
        {
            "OrderCancelled" => $"Đã hoàn {points} Xu vì đơn hàng #{orderCode} đã bị huỷ.",
            "OrderRefunded" => $"Đã hoàn {points} Xu vì đơn hàng #{orderCode} đã được hoàn tiền.",
            _ => $"Đã hoàn {points} Xu cho đơn hàng #{orderCode}."
        });
}

using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Domain.Exceptions;

public static class OrdersErrors
{
    public static readonly Error OrderNotFound = Error.NotFound("Orders.OrderNotFound", "Order not found");
    public static readonly Error CartEmpty = Error.Validation("Orders.CartEmpty", "Giỏ hàng trống");
    public static readonly Error AddressNotFound = Error.NotFound("Orders.AddressNotFound", "Địa chỉ không tồn tại");
    public static readonly Error InvalidPaymentMethod = Error.Validation("Orders.InvalidPaymentMethod", "Phương thức thanh toán không hợp lệ");
    public static readonly Error InvalidTransition = Error.Conflict("Orders.InvalidTransition", "Trạng thái đơn hàng không hợp lệ cho hành động này");
    public static readonly Error CancelNotAllowed = Error.Conflict("Orders.CancelNotAllowed", "Không thể hủy đơn ở trạng thái hiện tại");
    public static readonly Error InvalidIdempotencyKey = Error.Validation("Orders.InvalidIdempotencyKey", "Idempotency-Key không hợp lệ");
    public static readonly Error IdempotencyKeyReused = Error.Conflict("Orders.IdempotencyKeyReused", "Idempotency-Key đã được dùng cho một yêu cầu đặt hàng khác.");
    public static readonly Error IdempotencyKeyInProgress = Error.Conflict("Orders.IdempotencyKeyInProgress", "Yêu cầu đặt hàng trước đó với cùng Idempotency-Key vẫn đang xử lý.");
    public static readonly Error InvalidDateRange = Error.Validation("Orders.InvalidDateRange", "Khoảng thời gian không hợp lệ");

    public static Error ProductUnavailable(string productNames) =>
        Error.Conflict("Orders.ProductUnavailable", $"Sản phẩm không khả dụng: {productNames}");

    public static Error InvalidCoupon(string message) =>
        Error.Validation("Orders.InvalidCoupon", message);

    public static Error InsufficientStock(string productNames) =>
        Error.Conflict("Orders.InsufficientStock", $"Không đủ hàng cho: {productNames}. Vui lòng cập nhật giỏ hàng.");
}

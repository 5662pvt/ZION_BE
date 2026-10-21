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

    public static readonly Error ReturnRequestNotFound = Error.NotFound("Orders.ReturnRequestNotFound", "Không tìm thấy yêu cầu trả hàng");
    public static readonly Error ReturnNotEligible = Error.Conflict("Orders.ReturnNotEligible", "Đơn hàng chưa hoàn tất, không thể yêu cầu trả hàng");
    public static readonly Error ReturnWindowExpired = Error.Conflict("Orders.ReturnWindowExpired", "Đã quá thời hạn trả hàng");
    public static readonly Error ReturnAlreadyRequested = Error.Conflict("Orders.ReturnAlreadyRequested", "Đơn hàng đã có yêu cầu trả hàng đang xử lý");
    public static readonly Error ReturnInvalidTransition = Error.Conflict("Orders.ReturnInvalidTransition", "Trạng thái yêu cầu trả hàng không hợp lệ cho hành động này");
    public static readonly Error ReturnRejectReasonRequired = Error.Validation("Orders.ReturnRejectReasonRequired", "Cần nhập lý do từ chối");
    public static readonly Error InvalidReturnReason = Error.Validation("Orders.InvalidReturnReason", "Lý do trả hàng không hợp lệ");

    public static Error ProductUnavailable(string productNames) =>
        Error.Conflict("Orders.ProductUnavailable", $"Sản phẩm không khả dụng: {productNames}");

    public static Error InvalidCoupon(string message) =>
        Error.Validation("Orders.InvalidCoupon", message);

    public static Error InsufficientStock(string productNames) =>
        Error.Conflict("Orders.InsufficientStock", $"Không đủ hàng cho: {productNames}. Vui lòng cập nhật giỏ hàng.");
}

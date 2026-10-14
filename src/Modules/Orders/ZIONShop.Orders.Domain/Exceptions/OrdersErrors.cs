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

    public static Error ProductUnavailable(string productNames) =>
        Error.Conflict("Orders.ProductUnavailable", $"Sản phẩm không khả dụng: {productNames}");

    public static Error InvalidCoupon(string message) =>
        Error.Validation("Orders.InvalidCoupon", message);
}

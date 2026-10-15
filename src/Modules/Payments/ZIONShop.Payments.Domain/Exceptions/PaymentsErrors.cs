using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Domain.Exceptions;

public static class PaymentsErrors
{
    public static readonly Error OrderNotFound = Error.NotFound("Payments.OrderNotFound", "Order not found");
    public static readonly Error PaymentNotFound = Error.NotFound("Payments.PaymentNotFound", "Payment not found");
    public static readonly Error OrderNotPayable = Error.Conflict("Payments.OrderNotPayable", "Đơn hàng chưa sẵn sàng thanh toán");
    public static readonly Error OrderCancelled = Error.Validation("Payments.OrderCancelled", "Đơn hàng đã bị hủy");
    public static readonly Error OrderAlreadyPaid = Error.Conflict("Payments.OrderAlreadyPaid", "Đơn hàng đã được thanh toán");
    public static readonly Error PaymentNotPending = Error.Conflict("Payments.PaymentNotPending", "Giao dịch không còn ở trạng thái chờ xử lý");
    public static readonly Error InvalidSignature = Error.Unauthorized("Payments.InvalidSignature", "Chữ ký không hợp lệ");
    public static readonly Error InvalidWebhookPayload = Error.Validation("Payments.InvalidWebhookPayload", "Payload webhook không hợp lệ");
    public static readonly Error InvalidProjectionTransition = Error.Conflict("Payments.InvalidProjectionTransition", "Invalid projection state transition");
    public static readonly Error SimulationNotAvailable = Error.NotFound("Payments.SimulationNotAvailable", "Not found");
    public static readonly Error InvalidSimulationResult = Error.Validation("Payments.InvalidSimulationResult", "result phải là 'success' hoặc 'failure'");
    public static readonly Error InvalidChannel = Error.Validation("Payments.InvalidChannel", "channel phải là 'Card', 'EWallet' hoặc 'BankTransfer'");
}

using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Domain.Exceptions;

public static class LoyaltyErrors
{
    public static readonly Error InsufficientPoints = Error.Validation("Loyalty.InsufficientPoints", "Số dư Xu không đủ để thực hiện điều chỉnh này");
    public static readonly Error InvalidAdjustment = Error.Validation("Loyalty.InvalidAdjustment", "Số điểm điều chỉnh không hợp lệ");
    public static readonly Error ReasonRequired = Error.Validation("Loyalty.ReasonRequired", "Vui lòng nhập lý do điều chỉnh");
}

using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Domain.Exceptions;

public static class AdminErrors
{
    public static readonly Error InvalidDateFormat = Error.Validation("Admin.InvalidDateFormat", "Ngày không hợp lệ, định dạng yyyy-MM-dd");
    public static readonly Error InvalidRange = Error.Validation("Admin.InvalidRange", "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc");
    public static readonly Error RangeTooLarge = Error.Validation("Admin.RangeTooLarge", "Khoảng thời gian không được vượt quá 366 ngày");
}

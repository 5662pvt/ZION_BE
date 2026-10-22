using FluentValidation;

namespace ZIONShop.Chat.Application.Features.SendStaffMessage;

public class SendStaffMessageCommandValidator : AbstractValidator<SendStaffMessageCommand>
{
    private static readonly string[] AllowedTypes = { "Text", "Product", "Order" };

    public SendStaffMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.Type).Must(t => AllowedTypes.Contains(t)).WithMessage("Loại tin nhắn không hợp lệ");

        RuleFor(x => x.Text).NotEmpty().WithMessage("Vui lòng nhập nội dung tin nhắn")
            .When(x => x.Type == "Text");

        RuleFor(x => x.Text).MaximumLength(2000).WithMessage("Nội dung tin nhắn quá dài")
            .When(x => x.Text is not null);

        RuleFor(x => x.ProductId).NotNull().WithMessage("Vui lòng chọn sản phẩm")
            .When(x => x.Type == "Product");

        RuleFor(x => x.OrderId).NotNull().WithMessage("Vui lòng chọn đơn hàng")
            .When(x => x.Type == "Order");
    }
}

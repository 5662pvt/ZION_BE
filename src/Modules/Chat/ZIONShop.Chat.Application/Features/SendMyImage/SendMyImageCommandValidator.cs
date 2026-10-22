using FluentValidation;

namespace ZIONShop.Chat.Application.Features.SendMyImage;

public class SendMyImageCommandValidator : AbstractValidator<SendMyImageCommand>
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

    public SendMyImageCommandValidator()
    {
        RuleFor(x => x.ContentType).Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Ảnh không hợp lệ, chỉ hỗ trợ JPG, PNG hoặc WEBP");
        RuleFor(x => x.Length).InclusiveBetween(1, 5 * 1024 * 1024)
            .WithMessage("Ảnh vượt quá kích thước cho phép (tối đa 5MB)");
        RuleFor(x => x.Caption).MaximumLength(500);
    }
}

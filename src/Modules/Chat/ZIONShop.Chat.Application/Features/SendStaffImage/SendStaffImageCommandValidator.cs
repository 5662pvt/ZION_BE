using FluentValidation;

namespace ZIONShop.Chat.Application.Features.SendStaffImage;

public class SendStaffImageCommandValidator : AbstractValidator<SendStaffImageCommand>
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };

    public SendStaffImageCommandValidator()
    {
        RuleFor(x => x.ConversationId).NotEmpty();
        RuleFor(x => x.ContentType).Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Ảnh không hợp lệ, chỉ hỗ trợ JPG, PNG hoặc WEBP");
        RuleFor(x => x.Length).InclusiveBetween(1, 5 * 1024 * 1024)
            .WithMessage("Ảnh vượt quá kích thước cho phép (tối đa 5MB)");
        RuleFor(x => x.Caption).MaximumLength(500);
    }
}

using ZIONShop.Chat.Application.Features.SendMyImage;

namespace ZIONShop.Chat.Tests.Features;

public class SendMyImageCommandValidatorTests
{
    private readonly SendMyImageCommandValidator _validator = new();

    private static SendMyImageCommand Build(string contentType = "image/png", long length = 1024, string? caption = null) =>
        new(Guid.NewGuid(), Stream.Null, "file.png", contentType, length, caption);

    [Fact]
    public void Should_Fail_When_ContentTypeIsNotAllowed()
    {
        var result = _validator.Validate(Build(contentType: "application/pdf"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_LengthExceedsMax()
    {
        var result = _validator.Validate(Build(length: 6 * 1024 * 1024));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_CaptionExceedsMaxLength()
    {
        var result = _validator.Validate(Build(caption: new string('a', 501)));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_When_Valid()
    {
        var result = _validator.Validate(Build(caption: "nice"));

        result.IsValid.Should().BeTrue();
    }
}

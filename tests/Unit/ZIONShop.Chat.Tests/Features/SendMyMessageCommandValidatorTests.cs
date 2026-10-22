using ZIONShop.Chat.Application.Features.SendMyMessage;

namespace ZIONShop.Chat.Tests.Features;

public class SendMyMessageCommandValidatorTests
{
    private readonly SendMyMessageCommandValidator _validator = new();

    [Fact]
    public void Should_Fail_When_TextIsEmpty_And_TypeIsText()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Text", "   ", null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_TextExceedsMaxLength()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Text", new string('a', 2001), null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_TypeIsInvalid()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Image", "hi", null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_TypeIsProduct_And_ProductIdMissing()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Product", null, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_TypeIsOrder_And_OrderIdMissing()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Order", null, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_When_TextMessageIsValid()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Text", "Xin chào", null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Should_Pass_When_ProductCardWithProductId()
    {
        var result = _validator.Validate(new SendMyMessageCommand(Guid.NewGuid(), "Product", null, Guid.NewGuid(), null));

        result.IsValid.Should().BeTrue();
    }
}

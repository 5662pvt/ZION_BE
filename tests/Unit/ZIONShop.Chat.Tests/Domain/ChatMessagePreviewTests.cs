using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Enums;

namespace ZIONShop.Chat.Tests.Domain;

public class ChatMessagePreviewTests
{
    [Fact]
    public void BuildPreview_Should_ReturnText_When_TypeIsText()
    {
        ChatMessage.BuildPreview(ChatMessageType.Text, "hello world").Should().Be("hello world");
    }

    [Fact]
    public void BuildPreview_Should_ReturnPlaceholder_When_TypeIsImage()
    {
        ChatMessage.BuildPreview(ChatMessageType.Image, null).Should().Be("[Hình ảnh]");
    }

    [Fact]
    public void BuildPreview_Should_IncludeProductName_When_TypeIsProduct()
    {
        ChatMessage.BuildPreview(ChatMessageType.Product, null, productName: "Áo thun").Should().Be("[Sản phẩm] Áo thun");
    }

    [Fact]
    public void BuildPreview_Should_IncludeOrderCode_When_TypeIsOrder()
    {
        ChatMessage.BuildPreview(ChatMessageType.Order, null, orderCode: "DH001").Should().Be("[Đơn hàng] #DH001");
    }

    [Fact]
    public void BuildPreview_Should_TruncateTo120Characters()
    {
        var longText = new string('a', 200);

        var preview = ChatMessage.BuildPreview(ChatMessageType.Text, longText);

        preview.Length.Should().Be(120);
    }
}

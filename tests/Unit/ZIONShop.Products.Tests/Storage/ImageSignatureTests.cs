using ZIONShop.Products.Infrastructure.Storage;

namespace ZIONShop.Products.Tests.Storage;

public class ImageSignatureTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    private static readonly byte[] WebpBytes = { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0x00, 0x00, 0x00, 0x00, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };
    private static readonly byte[] SvgBytes = System.Text.Encoding.UTF8.GetBytes("<svg onload=alert(1)>");
    private static readonly byte[] HtmlBytes = System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");
    private static readonly byte[] GifRenamedAsOtherBytes = System.Text.Encoding.UTF8.GetBytes("GIF89a");

    [Fact]
    public void Detect_Should_ReturnJpeg_When_HeaderIsJpegMagicBytes()
        => ImageSignature.Detect(JpegBytes).Should().Be("image/jpeg");

    [Fact]
    public void Detect_Should_ReturnPng_When_HeaderIsPngMagicBytes()
        => ImageSignature.Detect(PngBytes).Should().Be("image/png");

    [Fact]
    public void Detect_Should_ReturnWebp_When_HeaderIsRiffWebpMagicBytes()
        => ImageSignature.Detect(WebpBytes).Should().Be("image/webp");

    [Fact]
    public void Detect_Should_ReturnNull_When_HeaderIsSvg()
        => ImageSignature.Detect(SvgBytes).Should().BeNull();

    [Fact]
    public void Detect_Should_ReturnNull_When_HeaderIsHtml()
        => ImageSignature.Detect(HtmlBytes).Should().BeNull();

    [Fact]
    public void Detect_Should_ReturnNull_When_HeaderIsGif()
        => ImageSignature.Detect(GifRenamedAsOtherBytes).Should().BeNull();

    [Fact]
    public void Detect_Should_ReturnNull_When_HeaderIsEmpty()
        => ImageSignature.Detect(Array.Empty<byte>()).Should().BeNull();

    [Theory]
    [InlineData("folder-1")]
    [InlineData("chat")]
    [InlineData("products")]
    public void IsValidFolderName_Should_ReturnTrue_When_OnlyLettersDigitsDashUnderscore(string folder)
        => ImageSignature.IsValidFolderName(folder).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("../products")]
    [InlineData("chat/evil")]
    [InlineData("chat evil")]
    public void IsValidFolderName_Should_ReturnFalse_When_ContainsPathOrWhitespaceCharacters(string folder)
        => ImageSignature.IsValidFolderName(folder).Should().BeFalse();
}

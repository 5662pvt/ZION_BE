using Microsoft.Extensions.Options;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Application.Options;
using ZIONShop.Products.Infrastructure.Storage;

namespace ZIONShop.Products.Tests.Storage;

public class LocalImageStorageTests : IDisposable
{
    // 1x1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private static readonly byte[] SvgWithScript = System.Text.Encoding.UTF8.GetBytes("<svg onload=\"alert(1)\"></svg>");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "zion-image-storage-tests", Guid.NewGuid().ToString("N"));

    private class FakeUploadsRootProvider : IUploadsRootProvider
    {
        public FakeUploadsRootProvider(string rootPath) => RootPath = rootPath;
        public string RootPath { get; }
    }

    private LocalImageStorage CreateStorage() =>
        new(new FakeUploadsRootProvider(_root), Options.Create(new ImageStorageOptions()));

    [Fact]
    public async Task UploadAsync_Should_StoreWithDetectedExtension_When_FilenameClaimsSvg_But_BytesArePng()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(TinyPng);

        var result = await storage.UploadAsync(content, "fake.svg", "image/png", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PublicId.Should().EndWith(".png");
        result.Value.Url.Should().EndWith(".png");
        File.Exists(Path.Combine(_root, result.Value.PublicId.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_Should_Fail_When_BytesAreSvgScript_RegardlessOfClaimedContentType()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(SvgWithScript);

        var result = await storage.UploadAsync(content, "x.svg", "image/png", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.InvalidImageUpload");
        if (Directory.Exists(_root))
            Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAsync_Should_UseGivenFolder_When_ProvidedAndValid()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(TinyPng);

        var result = await storage.UploadAsync(content, "ignored.png", "image/png", CancellationToken.None, folder: "chat");

        result.IsSuccess.Should().BeTrue();
        result.Value.PublicId.Should().StartWith("chat/");
    }

    [Fact]
    public async Task UploadAsync_Should_Fail_When_FolderNameIsInvalid()
    {
        var storage = CreateStorage();
        using var content = new MemoryStream(TinyPng);

        var result = await storage.UploadAsync(content, "x.png", "image/png", CancellationToken.None, folder: "../evil");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Products.InvalidImageUpload");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

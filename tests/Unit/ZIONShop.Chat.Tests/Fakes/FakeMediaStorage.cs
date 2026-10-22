using ZIONShop.Contracts.Media;

namespace ZIONShop.Chat.Tests.Fakes;

internal sealed class FakeMediaStorage : IMediaStorage
{
    public bool ShouldFail { get; set; }

    public Task<(StoredMedia? Media, string? Error)> UploadImageAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default) =>
        Task.FromResult(ShouldFail
            ? (default(StoredMedia), "upload failed")
            : (new StoredMedia($"https://cdn.test.local/{folder}/{fileName}", $"{folder}/{fileName}"), default(string?)));
}

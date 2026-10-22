using ZIONShop.Chat.Application.Features.SendMyImage;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Infrastructure.Repositories;
using ZIONShop.Chat.Tests.Fakes;
using ZIONShop.Chat.Tests.TestSupport;

namespace ZIONShop.Chat.Tests.Features;

public class SendMyImageCommandHandlerTests
{
    private sealed class Harness
    {
        public FakeMediaStorage Media = new();
        public FakeRealtimeEventPublisher Realtime = new();
    }

    private static (SendMyImageCommandHandler Handler, Harness Deps) BuildHandler()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var messages = new ChatMessageRepository(db);
        var contacts = new FakeUserContactReader();
        var media = new FakeMediaStorage();
        var realtime = new FakeRealtimeEventPublisher();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var provisioning = new ConversationProvisioningService(conversations, contacts, db, time);
        var handler = new SendMyImageCommandHandler(provisioning, messages, db, media, realtime, time);

        return (handler, new Harness { Media = media, Realtime = realtime });
    }

    private static SendMyImageCommand Build(string contentType = "image/png", long length = 1024, string? caption = null) =>
        new(Guid.NewGuid(), Stream.Null, "file.png", contentType, length, caption);

    [Fact]
    public async Task Handle_Should_Fail_When_ContentTypeIsInvalid()
    {
        var (handler, _) = BuildHandler();

        var result = await handler.Handle(Build(contentType: "application/pdf"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.InvalidImage);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_TooLarge()
    {
        var (handler, _) = BuildHandler();

        var result = await handler.Handle(Build(length: 6 * 1024 * 1024), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.ImageTooLarge);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_MediaUploadFails()
    {
        var (handler, deps) = BuildHandler();
        deps.Media.ShouldFail = true;

        var result = await handler.Handle(Build(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.InvalidImage);
    }

    [Fact]
    public async Task Handle_Should_CreateImageMessage_When_Valid()
    {
        var (handler, deps) = BuildHandler();

        var result = await handler.Handle(Build(caption: "nice shot"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("Image");
        result.Value.ImageUrl.Should().NotBeNullOrEmpty();
        result.Value.Text.Should().Be("nice shot");
        deps.Realtime.UserPublishes.Should().ContainSingle();
        deps.Realtime.StaffPublishes.Should().ContainSingle();
    }
}

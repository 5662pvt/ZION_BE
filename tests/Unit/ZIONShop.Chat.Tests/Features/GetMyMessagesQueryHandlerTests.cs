using ZIONShop.Chat.Application.Features.GetMyMessages;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Chat.Infrastructure.Repositories;
using ZIONShop.Chat.Tests.TestSupport;

namespace ZIONShop.Chat.Tests.Features;

public class GetMyMessagesQueryHandlerTests
{
    [Fact]
    public async Task Handle_Should_UseIdAsTieBreaker_When_TwoMessagesShareTheSameCreatedAt()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var messages = new ChatMessageRepository(db);
        var customerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var conversation = Conversation.Create(customerId, "Nguyen Van A", "a@test.local", now);
        db.Conversations.Add(conversation);

        var messageA = ChatMessage.Create(conversation.Id, customerId, ChatSenderRole.Customer, ChatMessageType.Text, "first", null, null, null, null, null, now);
        var messageB = ChatMessage.Create(conversation.Id, customerId, ChatSenderRole.Customer, ChatMessageType.Text, "second", null, null, null, null, null, now);
        db.Messages.AddRange(messageA, messageB);
        await db.SaveChangesAsync();

        // Handler orders by (CreatedAt desc, Id desc); the larger Id sorts first among the identical timestamps.
        var (newer, older) = string.CompareOrdinal(messageA.Id.ToString(), messageB.Id.ToString()) > 0
            ? (messageA, messageB)
            : (messageB, messageA);

        var handler = new GetMyMessagesQueryHandler(conversations, messages);

        var firstPage = await handler.Handle(new GetMyMessagesQuery(customerId, null, null, 1), default);
        firstPage.IsSuccess.Should().BeTrue();
        firstPage.Value.Items.Should().ContainSingle(m => m.Id == newer.Id);
        firstPage.Value.HasMore.Should().BeTrue();

        var secondPage = await handler.Handle(new GetMyMessagesQuery(customerId, now, newer.Id, 1), default);
        secondPage.IsSuccess.Should().BeTrue();
        secondPage.Value.Items.Should().ContainSingle(m => m.Id == older.Id);
        secondPage.Value.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_FallBackToCreatedAtOnly_When_BeforeIdIsNotProvided()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var messages = new ChatMessageRepository(db);
        var customerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var conversation = Conversation.Create(customerId, "Nguyen Van A", "a@test.local", now);
        db.Conversations.Add(conversation);

        var older = ChatMessage.Create(conversation.Id, customerId, ChatSenderRole.Customer, ChatMessageType.Text, "older", null, null, null, null, null, now.AddMinutes(-1));
        var newer = ChatMessage.Create(conversation.Id, customerId, ChatSenderRole.Customer, ChatMessageType.Text, "newer", null, null, null, null, null, now);
        db.Messages.AddRange(older, newer);
        await db.SaveChangesAsync();

        var handler = new GetMyMessagesQueryHandler(conversations, messages);

        var page = await handler.Handle(new GetMyMessagesQuery(customerId, now, null, 10), default);

        page.IsSuccess.Should().BeTrue();
        page.Value.Items.Should().ContainSingle(m => m.Id == older.Id);
    }
}

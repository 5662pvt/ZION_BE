using ZIONShop.Chat.Application.Features.MarkConversationReadByStaff;
using ZIONShop.Chat.Application.Features.MarkMyConversationRead;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Enums;
using ZIONShop.Chat.Infrastructure.Repositories;
using ZIONShop.Chat.Tests.Fakes;
using ZIONShop.Chat.Tests.TestSupport;

namespace ZIONShop.Chat.Tests.Features;

public class MarkReadCommandHandlerTests
{
    [Fact]
    public async Task MarkMyConversationRead_Should_ResetCustomerUnread_And_PublishToStaff()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var contacts = new FakeUserContactReader();
        var realtime = new FakeRealtimeEventPublisher();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provisioning = new ConversationProvisioningService(conversations, contacts, db, time);

        var userId = Guid.NewGuid();
        var conversation = Conversation.Create(userId, "Nguyen Van A", "a@test.local", DateTime.UtcNow);
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var handler = new MarkMyConversationReadCommandHandler(provisioning, db, realtime, time);
        var result = await handler.Handle(new MarkMyConversationReadCommand(userId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerUnread.Should().Be(0);
        realtime.StaffPublishes.Should().ContainSingle(p => p.Event == "chat:read");
    }

    [Fact]
    public async Task MarkConversationReadByStaff_Should_ResetStaffUnread_And_PublishToCustomer()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var realtime = new FakeRealtimeEventPublisher();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var customerId = Guid.NewGuid();
        var conversation = Conversation.Create(customerId, "Nguyen Van A", "a@test.local", DateTime.UtcNow);
        conversation.AddMessage(customerId, ChatSenderRole.Customer, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var handler = new MarkConversationReadByStaffCommandHandler(conversations, db, realtime, time);
        var result = await handler.Handle(new MarkConversationReadByStaffCommand(conversation.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.StaffUnread.Should().Be(0);
        realtime.UserPublishes.Should().ContainSingle(p => p.UserId == customerId && p.Event == "chat:read");
    }
}

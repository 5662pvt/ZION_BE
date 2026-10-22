using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Enums;

namespace ZIONShop.Chat.Tests.Domain;

public class ConversationTests
{
    private static Conversation CreateConversation() =>
        Conversation.Create(Guid.NewGuid(), "Nguyen Van A", "a@test.local", DateTime.UtcNow);

    [Fact]
    public void AddMessage_Should_IncrementStaffUnread_When_SenderIsCustomer()
    {
        var conversation = CreateConversation();

        conversation.AddMessage(conversation.CustomerId, ChatSenderRole.Customer, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);

        conversation.StaffUnread.Should().Be(1);
        conversation.CustomerUnread.Should().Be(0);
        conversation.LastMessageSender.Should().Be(ChatSenderRole.Customer);
    }

    [Fact]
    public void AddMessage_Should_IncrementCustomerUnread_When_SenderIsStaff()
    {
        var conversation = CreateConversation();

        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "reply", null, null, null, null, null, "reply", DateTime.UtcNow);

        conversation.CustomerUnread.Should().Be(1);
        conversation.StaffUnread.Should().Be(0);
        conversation.LastMessageSender.Should().Be(ChatSenderRole.Staff);
    }

    [Fact]
    public void AddMessage_Should_UpdateLastMessageAtAndPreview()
    {
        var conversation = CreateConversation();
        var now = DateTime.UtcNow.AddMinutes(5);

        conversation.AddMessage(conversation.CustomerId, ChatSenderRole.Customer, ChatMessageType.Text, "hello there", null, null, null, null, null, "hello there", now);

        conversation.LastMessageAt.Should().Be(now);
        conversation.LastMessagePreview.Should().Be("hello there");
    }

    [Fact]
    public void MarkReadBy_Customer_Should_ResetCustomerUnread_And_SetCustomerLastReadAt()
    {
        var conversation = CreateConversation();
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        conversation.MarkReadBy(ChatSenderRole.Customer, now);

        conversation.CustomerUnread.Should().Be(0);
        conversation.CustomerLastReadAt.Should().Be(now);
    }

    [Fact]
    public void MarkReadBy_Staff_Should_ResetStaffUnread_And_SetStaffLastReadAt()
    {
        var conversation = CreateConversation();
        conversation.AddMessage(conversation.CustomerId, ChatSenderRole.Customer, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        var now = DateTime.UtcNow;

        conversation.MarkReadBy(ChatSenderRole.Staff, now);

        conversation.StaffUnread.Should().Be(0);
        conversation.StaffLastReadAt.Should().Be(now);
    }

    [Fact]
    public void IsAlreadyReadBy_Should_BeTrue_When_NoMessagesYet()
    {
        var conversation = CreateConversation();

        conversation.IsAlreadyReadBy(ChatSenderRole.Customer).Should().BeTrue();
        conversation.IsAlreadyReadBy(ChatSenderRole.Staff).Should().BeTrue();
    }

    [Fact]
    public void IsAlreadyReadBy_Should_BeFalse_When_UnreadMessageExists()
    {
        var conversation = CreateConversation();
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);

        conversation.IsAlreadyReadBy(ChatSenderRole.Customer).Should().BeFalse();
    }

    [Fact]
    public void IsAlreadyReadBy_Should_BeTrue_When_AlreadyReadUpToLastMessage()
    {
        var conversation = CreateConversation();
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        conversation.MarkReadBy(ChatSenderRole.Customer, DateTime.UtcNow);

        conversation.IsAlreadyReadBy(ChatSenderRole.Customer).Should().BeTrue();
    }

    [Fact]
    public void IsAlreadyReadBy_Should_BeFalse_When_NewMessageArrivedAfterLastRead()
    {
        var conversation = CreateConversation();
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        conversation.MarkReadBy(ChatSenderRole.Customer, DateTime.UtcNow);
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "again", null, null, null, null, null, "again", DateTime.UtcNow.AddMinutes(1));

        conversation.IsAlreadyReadBy(ChatSenderRole.Customer).Should().BeFalse();
    }

    [Fact]
    public void MarkReadBy_Should_NotAffectTheOtherSidesCounters()
    {
        var conversation = CreateConversation();
        conversation.AddMessage(conversation.CustomerId, ChatSenderRole.Customer, ChatMessageType.Text, "hi", null, null, null, null, null, "hi", DateTime.UtcNow);
        conversation.AddMessage(Guid.NewGuid(), ChatSenderRole.Staff, ChatMessageType.Text, "reply", null, null, null, null, null, "reply", DateTime.UtcNow);

        conversation.MarkReadBy(ChatSenderRole.Customer, DateTime.UtcNow);

        conversation.StaffUnread.Should().Be(1);
        conversation.CustomerUnread.Should().Be(0);
    }
}

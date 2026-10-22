using ZIONShop.Chat.Application.Features.SendStaffMessage;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Infrastructure.Persistence;
using ZIONShop.Chat.Infrastructure.Repositories;
using ZIONShop.Chat.Tests.Fakes;
using ZIONShop.Chat.Tests.TestSupport;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;

namespace ZIONShop.Chat.Tests.Features;

public class SendStaffMessageCommandHandlerTests
{
    private static OrderDetails BuildOrder(Guid orderId, Guid userId, string code = "DH001") =>
        new(orderId, userId, code, "Confirmed", "COD", 100_000m, 0m, null, null, 0m, 100_000m, "VND",
            "A", "0900000000", "Address", null, DateTime.UtcNow, new[] { new OrderDetailsLine("Sản phẩm A", 1, 100_000m, 100_000m) });

    private sealed class Harness
    {
        public ChatDbContext Db = null!;
        public ConversationRepository Conversations = null!;
        public FakeProductPriceLookup Products = new();
        public FakeOrderDetailsReader Orders = new();
        public FakeRealtimeEventPublisher Realtime = new();
    }

    private static (SendStaffMessageCommandHandler Handler, Harness Deps, Conversation Conversation) BuildHandler()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var messages = new ChatMessageRepository(db);
        var products = new FakeProductPriceLookup();
        var orders = new FakeOrderDetailsReader();
        var realtime = new FakeRealtimeEventPublisher();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cards = new ChatCardService(products, orders);

        var conversation = Conversation.Create(Guid.NewGuid(), "Nguyen Van A", "a@test.local", DateTime.UtcNow);
        db.Conversations.Add(conversation);
        db.SaveChanges();

        var handler = new SendStaffMessageCommandHandler(conversations, messages, db, cards, realtime, time);

        return (handler, new Harness { Db = db, Conversations = conversations, Products = products, Orders = orders, Realtime = realtime }, conversation);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_ConversationNotFound()
    {
        var (handler, _, _) = BuildHandler();

        var result = await handler.Handle(new SendStaffMessageCommand(Guid.NewGuid(), Guid.NewGuid(), "Text", "hi", null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.ConversationNotFound);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_OrderBelongsToDifferentCustomer()
    {
        var (handler, deps, conversation) = BuildHandler();
        var orderId = Guid.NewGuid();
        deps.Orders.With(BuildOrder(orderId, Guid.NewGuid()));

        var result = await handler.Handle(new SendStaffMessageCommand(Guid.NewGuid(), conversation.Id, "Order", null, null, orderId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.OrderNotFound);
    }

    [Fact]
    public async Task Handle_Should_AttachOrderCard_When_OrderBelongsToConversationCustomer()
    {
        var (handler, deps, conversation) = BuildHandler();
        var orderId = Guid.NewGuid();
        deps.Orders.With(BuildOrder(orderId, conversation.CustomerId));

        var result = await handler.Handle(new SendStaffMessageCommand(Guid.NewGuid(), conversation.Id, "Order", null, null, orderId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order!.Code.Should().Be("DH001");
        result.Value.SenderName.Should().Be("ZIONShop");
    }

    [Fact]
    public async Task Handle_Should_AllowUnpublishedProduct_ForStaff()
    {
        var (handler, deps, conversation) = BuildHandler();
        var productId = Guid.NewGuid();
        deps.Products.With(new ProductSummaryInfo(productId, "Áo thun Draft", "ao-thun-draft", 100_000m, "VND", null, IsPublished: false));

        var result = await handler.Handle(new SendStaffMessageCommand(Guid.NewGuid(), conversation.Id, "Product", null, productId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Product!.Name.Should().Be("Áo thun Draft");
    }

    [Fact]
    public async Task Handle_Should_PublishToCustomer_And_Staff()
    {
        var (handler, deps, conversation) = BuildHandler();

        var result = await handler.Handle(new SendStaffMessageCommand(Guid.NewGuid(), conversation.Id, "Text", "reply", null, null), default);

        result.IsSuccess.Should().BeTrue();
        deps.Realtime.UserPublishes.Should().ContainSingle(p => p.UserId == conversation.CustomerId);
        deps.Realtime.StaffPublishes.Should().ContainSingle();
    }
}

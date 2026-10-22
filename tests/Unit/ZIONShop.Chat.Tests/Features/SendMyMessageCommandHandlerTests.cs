using ZIONShop.Chat.Application.Features.SendMyMessage;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Exceptions;
using ZIONShop.Chat.Infrastructure.Persistence;
using ZIONShop.Chat.Infrastructure.Repositories;
using ZIONShop.Chat.Tests.Fakes;
using ZIONShop.Chat.Tests.TestSupport;
using ZIONShop.Contracts.Auth;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;

namespace ZIONShop.Chat.Tests.Features;

public class SendMyMessageCommandHandlerTests
{
    private static OrderDetails BuildOrder(Guid orderId, Guid userId, string code = "DH001") =>
        new(orderId, userId, code, "Confirmed", "COD", 100_000m, 0m, null, null, 0m, 100_000m, "VND",
            "A", "0900000000", "Address", null, DateTime.UtcNow, new[] { new OrderDetailsLine("Sản phẩm A", 2, 50_000m, 100_000m) });

    private sealed class Harness
    {
        public ConversationRepository Conversations = null!;
        public ChatMessageRepository Messages = null!;
        public FakeUserContactReader Contacts = new();
        public FakeProductPriceLookup Products = new();
        public FakeOrderDetailsReader Orders = new();
        public FakeRealtimeEventPublisher Realtime = new();
        public FakeTimeProvider Time = new(DateTimeOffset.UtcNow);
    }

    private static (ChatDbContext Db, SendMyMessageCommandHandler Handler, Harness Deps) BuildHandler()
    {
        var db = ChatTestDb.Create();
        var conversations = new ConversationRepository(db);
        var messages = new ChatMessageRepository(db);
        var contacts = new FakeUserContactReader();
        var products = new FakeProductPriceLookup();
        var orders = new FakeOrderDetailsReader();
        var realtime = new FakeRealtimeEventPublisher();
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var provisioning = new ConversationProvisioningService(conversations, contacts, db, time);
        var cards = new ChatCardService(products, orders);

        var handler = new SendMyMessageCommandHandler(provisioning, messages, db, cards, realtime, time);

        return (db, handler, new Harness { Conversations = conversations, Messages = messages, Contacts = contacts, Products = products, Orders = orders, Realtime = realtime, Time = time });
    }

    [Fact]
    public async Task Handle_Should_CreateTextMessage_And_PublishToBothSides()
    {
        var (db, handler, deps) = BuildHandler();
        var userId = Guid.NewGuid();
        deps.Contacts.With(new UserContact(userId, "a@test.local", "Nguyen Van A"));

        var result = await handler.Handle(new SendMyMessageCommand(userId, "Text", "Xin chào", null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Xin chào");
        result.Value.SenderName.Should().Be("Nguyen Van A");
        deps.Realtime.UserPublishes.Should().ContainSingle(p => p.UserId == userId && p.Event == "chat:message");
        deps.Realtime.StaffPublishes.Should().ContainSingle(p => p.Event == "chat:message");
    }

    [Fact]
    public async Task Handle_Should_Fail_When_TextIsEmpty()
    {
        var (_, handler, _) = BuildHandler();

        var result = await handler.Handle(new SendMyMessageCommand(Guid.NewGuid(), "Text", "   ", null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.EmptyMessage);
    }

    [Fact]
    public async Task Handle_Should_AttachProductCardSnapshot_When_TypeIsProduct()
    {
        var (_, handler, deps) = BuildHandler();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        deps.Products.With(new ProductSummaryInfo(productId, "Áo thun", "ao-thun", 100_000m, "VND", "https://img/1.png", IsPublished: true, MaxPrice: 150_000m));

        var result = await handler.Handle(new SendMyMessageCommand(userId, "Product", null, productId, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Product.Should().NotBeNull();
        result.Value.Product!.Name.Should().Be("Áo thun");
        result.Value.Product.MinPrice.Should().Be(100_000m);
        result.Value.Product.MaxPrice.Should().Be(150_000m);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_ProductIsNotPublished()
    {
        var (_, handler, deps) = BuildHandler();
        var productId = Guid.NewGuid();
        deps.Products.With(new ProductSummaryInfo(productId, "Áo thun", "ao-thun", 100_000m, "VND", null, IsPublished: false));

        var result = await handler.Handle(new SendMyMessageCommand(Guid.NewGuid(), "Product", null, productId, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.ProductNotFound);
    }

    [Fact]
    public async Task Handle_Should_Fail_When_OrderBelongsToAnotherCustomer()
    {
        var (_, handler, deps) = BuildHandler();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        deps.Orders.With(BuildOrder(orderId, otherUserId));

        var result = await handler.Handle(new SendMyMessageCommand(userId, "Order", null, null, orderId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ChatErrors.OrderNotFound);
    }

    [Fact]
    public async Task Handle_Should_AttachOrderCardSnapshot_When_OrderBelongsToCustomer()
    {
        var (_, handler, deps) = BuildHandler();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        deps.Orders.With(BuildOrder(orderId, userId));

        var result = await handler.Handle(new SendMyMessageCommand(userId, "Order", null, null, orderId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Order.Should().NotBeNull();
        result.Value.Order!.Code.Should().Be("DH001");
        result.Value.Order.ItemCount.Should().Be(2);
        result.Value.Order.FirstItemName.Should().Be("Sản phẩm A");
    }

    [Fact]
    public async Task Handle_Should_Succeed_When_RealtimePublisherThrows()
    {
        var (_, handler, deps) = BuildHandler();
        deps.Realtime.ShouldThrow = true;

        var result = await handler.Handle(new SendMyMessageCommand(Guid.NewGuid(), "Text", "hi", null, null), default);

        result.IsSuccess.Should().BeTrue();
    }
}

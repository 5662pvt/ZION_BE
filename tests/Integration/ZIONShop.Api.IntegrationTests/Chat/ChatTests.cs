using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Chat;

public record ChatProductCardData(Guid Id, string Name, string Slug, decimal MinPrice, decimal MaxPrice, string Currency, string? ImageUrl);
public record ChatOrderCardData(Guid Id, string Code, string Status, decimal Total, string Currency, string FirstItemName, int ItemCount);
public record ChatMessageData(Guid Id, Guid ConversationId, Guid SenderId, string SenderRole, string SenderName, string Type, string? Text, string? ImageUrl, ChatProductCardData? Product, ChatOrderCardData? Order, DateTime CreatedAt);
public record ChatMessagePageData(List<ChatMessageData> Items, bool HasMore);
public record ConversationData(Guid Id, Guid CustomerId, string CustomerName, string CustomerEmail, DateTime LastMessageAt, string LastMessagePreview, string LastMessageSender, int CustomerUnread, int StaffUnread, DateTime? CustomerLastReadAt, DateTime? StaffLastReadAt);
public record StaffUnreadCountData(int Conversations, int Messages);

[Collection(ApiTestCollection.Name)]
public class ChatTests
{
    // 1x1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly ApiFactory _factory;

    public ChatTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<ConversationData> GetMyConversationAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/chat/me");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ConversationData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<ChatMessageData> SendMyTextAsync(HttpClient client, string text)
    {
        var response = await client.PostAsJsonAsync("/api/v1/chat/me/messages", new { type = "Text", text });
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessageData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<HttpResponseMessage> SendMyMessageRawAsync(HttpClient client, string type, string? text = null, Guid? productId = null, Guid? orderId = null)
        => await client.PostAsJsonAsync("/api/v1/chat/me/messages", new { type, text, productId, orderId });

    [Fact]
    public async Task GetMe_Should_CreateConversationLazily_OnFirstCall()
    {
        var (customer, _, userId) = await CreateCustomerWithAddressAsync(_factory);

        var conversation = await GetMyConversationAsync(customer);

        conversation.CustomerId.Should().Be(userId);
        conversation.CustomerUnread.Should().Be(0);
        conversation.StaffUnread.Should().Be(0);
    }

    [Fact]
    public async Task SendMyText_Should_ShowUpInStaffList_With_StaffUnreadOne()
    {
        var admin = await AdminClientAsync();
        var (customer, _, userId) = await CreateCustomerWithAddressAsync(_factory);

        await SendMyTextAsync(customer, "Shop ơi áo này còn size M không?");

        var listResponse = await admin.GetAsync("/api/v1/admin/chat/conversations?unreadOnly=true");
        listResponse.EnsureSuccessStatusCode();
        var listEnvelope = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ConversationData>>>(ApiEnvelopeJson.Options);
        var conversation = listEnvelope!.Data!.Single(c => c.CustomerId == userId);
        conversation.StaffUnread.Should().Be(1);
        conversation.LastMessagePreview.Should().Be("Shop ơi áo này còn size M không?");

        var unreadCountResponse = await admin.GetAsync("/api/v1/admin/chat/unread-count");
        unreadCountResponse.EnsureSuccessStatusCode();
        var unreadCountEnvelope = await unreadCountResponse.Content.ReadFromJsonAsync<ApiEnvelope<StaffUnreadCountData>>(ApiEnvelopeJson.Options);
        unreadCountEnvelope!.Data!.Conversations.Should().BeGreaterThanOrEqualTo(1);
        unreadCountEnvelope.Data!.Messages.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task StaffReply_Should_AppearInCustomerMessages_With_CustomerUnreadOne()
    {
        var admin = await AdminClientAsync();
        var (customer, _, userId) = await CreateCustomerWithAddressAsync(_factory);
        await SendMyTextAsync(customer, "Hỏi về sản phẩm");

        var conversation = await GetMyConversationAsync(customer);

        var replyResponse = await admin.PostAsJsonAsync($"/api/v1/admin/chat/conversations/{conversation.Id}/messages", new { type = "Text", text = "Chào bạn, shop còn hàng nhé" });
        replyResponse.EnsureSuccessStatusCode();

        var myMessagesResponse = await customer.GetAsync("/api/v1/chat/me/messages");
        myMessagesResponse.EnsureSuccessStatusCode();
        var myMessages = await myMessagesResponse.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessagePageData>>(ApiEnvelopeJson.Options);
        myMessages!.Data!.Items.Should().Contain(m => m.Text == "Chào bạn, shop còn hàng nhé" && m.SenderRole == "Staff" && m.SenderName == "ZIONShop");

        var updated = await GetMyConversationAsync(customer);
        updated.CustomerUnread.Should().Be(1);
    }

    [Fact]
    public async Task MarkMyConversationRead_Should_ResetCustomerUnread()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        await SendMyTextAsync(customer, "hi");
        var conversation = await GetMyConversationAsync(customer);
        (await admin.PostAsJsonAsync($"/api/v1/admin/chat/conversations/{conversation.Id}/messages", new { type = "Text", text = "reply" })).EnsureSuccessStatusCode();

        var readResponse = await customer.PostAsync("/api/v1/chat/me/read", null);
        readResponse.EnsureSuccessStatusCode();
        var readEnvelope = await readResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConversationData>>(ApiEnvelopeJson.Options);

        readEnvelope!.Data!.CustomerUnread.Should().Be(0);
        readEnvelope.Data!.CustomerLastReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendMyMessage_Should_AttachOrderCard_When_OrderBelongsToCustomer()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);
        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var order = await PlaceOrderAsync(customer, addressId, "COD");

        var response = await SendMyMessageRawAsync(customer, "Order", orderId: order.Id);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessageData>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Order!.Code.Should().Be(order.Code);
    }

    [Fact]
    public async Task SendMyMessage_Should_ReturnNotFound_When_OrderBelongsToAnotherCustomer()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);
        var (ownerCustomer, ownerAddressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(ownerCustomer, productId, 1);
        var order = await PlaceOrderAsync(ownerCustomer, ownerAddressId, "COD");

        var (otherCustomer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await SendMyMessageRawAsync(otherCustomer, "Order", orderId: order.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors!.Should().Contain(e => e.Code == "Chat.OrderNotFound");
    }

    [Fact]
    public async Task SendMyImage_Should_ReturnImageMessage_With_Url()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "tiny.png");
        content.Add(new StringContent("hình ảnh test"), "caption");

        var response = await customer.PostAsync("/api/v1/chat/me/images", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessageData>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Type.Should().Be("Image");
        envelope.Data!.ImageUrl.Should().NotBeNullOrEmpty();
        envelope.Data!.Text.Should().Be("hình ảnh test");
    }

    [Fact]
    public async Task AdminChatEndpoints_Should_ReturnForbidden_When_CallerIsCustomer()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await customer.GetAsync("/api/v1/admin/chat/conversations");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMyMessages_Should_Paginate_With_BeforeCursor()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        for (var i = 0; i < 5; i++)
            await SendMyTextAsync(customer, $"message {i}");

        var firstPageResponse = await customer.GetAsync("/api/v1/chat/me/messages?limit=2");
        firstPageResponse.EnsureSuccessStatusCode();
        var firstPage = await firstPageResponse.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessagePageData>>(ApiEnvelopeJson.Options);
        firstPage!.Data!.Items.Should().HaveCount(2);
        firstPage.Data!.HasMore.Should().BeTrue();

        var before = firstPage.Data!.Items.Last().CreatedAt;
        var secondPageResponse = await customer.GetAsync($"/api/v1/chat/me/messages?limit=2&before={Uri.EscapeDataString(before.ToString("O"))}");
        secondPageResponse.EnsureSuccessStatusCode();
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessagePageData>>(ApiEnvelopeJson.Options);
        secondPage!.Data!.Items.Should().HaveCount(2);
        secondPage.Data!.Items.Should().NotContain(m => firstPage.Data!.Items.Select(x => x.Id).Contains(m.Id));
    }
}

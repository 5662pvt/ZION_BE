using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Chat;

[Collection(ApiTestCollection.Name)]
public class ChatSecurityTests
{
    // 1x1 transparent PNG.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private static readonly byte[] SvgWithScript = System.Text.Encoding.UTF8.GetBytes(
        "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(document.domain)</script></svg>");

    private readonly ApiFactory _factory;

    public ChatSecurityTests(ApiFactory factory) => _factory = factory;

    private record ConversationData(Guid Id, Guid CustomerId, string CustomerName, string CustomerEmail, DateTime LastMessageAt, string LastMessagePreview, string LastMessageSender, int CustomerUnread, int StaffUnread, DateTime? CustomerLastReadAt, DateTime? StaffLastReadAt);
    private record ChatMessageData(Guid Id, Guid ConversationId, Guid SenderId, string SenderRole, string SenderName, string Type, string? Text, string? ImageUrl, object? Product, object? Order, DateTime CreatedAt);

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

    [Fact]
    public async Task SendMyImage_Should_Return400_When_BytesAreSvgScript_DespiteClaimedPngContentTypeAndFilename()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(SvgWithScript);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "x.svg");

        var response = await customer.PostAsync("/api/v1/chat/me/images", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors!.Should().Contain(e => e.Code == "Chat.InvalidImage");
    }

    [Fact]
    public async Task SendMyImage_Should_Return200_And_ServeDetectedPngType_With_NosniffHeader_When_BytesAreARealPng()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(TinyPng);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "tiny.png");

        var response = await customer.PostAsync("/api/v1/chat/me/images", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ChatMessageData>>(ApiEnvelopeJson.Options);
        var url = envelope!.Data!.ImageUrl!;
        var path = Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri) ? absoluteUri.AbsolutePath : url;

        var imageResponse = await customer.GetAsync(path);

        imageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        imageResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        imageResponse.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
    }

    [Fact]
    public async Task GetMe_Should_ReturnSameConversation_When_TwoConcurrentFirstRequests_From_TheSameNewCustomer()
    {
        var (customer, _, userId) = await CreateCustomerWithAddressAsync(_factory);

        var responses = await Task.WhenAll(
            customer.GetAsync("/api/v1/chat/me"),
            customer.GetAsync("/api/v1/chat/me"));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var conversations = await Task.WhenAll(responses.Select(async r =>
            (await r.Content.ReadFromJsonAsync<ApiEnvelope<ConversationData>>(ApiEnvelopeJson.Options))!.Data!));

        conversations.Select(c => c.Id).Distinct().Should().ContainSingle();
        conversations.Should().OnlyContain(c => c.CustomerId == userId);
    }

    [Fact]
    public async Task SendMyMessage_Should_NotLoseUnreadIncrement_When_TwoCustomerSendsRaceOnTheSameConversation()
    {
        var admin = await AdminClientAsync();
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);
        // Pre-create the conversation so both concurrent sends race on the unread *counter* update
        // (finding 3), not on ConversationProvisioningService's lazy-create race (finding 2).
        var conversation = await GetMyConversationAsync(customer);

        var responses = await Task.WhenAll(
            customer.PostAsJsonAsync("/api/v1/chat/me/messages", new { type = "Text", text = "message 1" }),
            customer.PostAsJsonAsync("/api/v1/chat/me/messages", new { type = "Text", text = "message 2" }));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var detailResponse = await admin.GetAsync($"/api/v1/admin/chat/conversations/{conversation.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApiEnvelope<ConversationData>>(ApiEnvelopeJson.Options);

        detail!.Data!.StaffUnread.Should().Be(2);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Notifications.Infrastructure.Persistence;
using ZIONShop.Orders.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

[Collection(ApiTestCollection.Name)]
public class AdminSystemMessagesTests
{
    private record ModulesDto(List<string> Outbox, List<string> Inbox);

    private record OutboxMessageDto(
        Guid Id, string Module, string EventType, DateTime OccurredOn, DateTime? ProcessedOn,
        int Attempts, string Status, string? LastError);

    private record OutboxMessageDetailDto(
        Guid Id, string Module, string EventType, DateTime OccurredOn, DateTime? ProcessedOn,
        int Attempts, string Status, string? LastError, string? TraceParent, string Payload);

    private record InboxMessageDto(Guid MessageId, string Module, string Consumer, string EventType, DateTime ProcessedOn);

    private record RetryResultDto(Guid Id, string Status);

    private record RetryFailedResultDto(int Retried);

    private readonly ApiFactory _factory;

    public AdminSystemMessagesTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    /// <summary>
    /// A deliberately unresolvable "Type" string (rather than a real event's AssemblyQualifiedName) --
    /// a Pending row (Attempts reset to 0 by the retry endpoints under test) is still live in the
    /// OutboxPublisherService's claim query and WILL be picked up by the real background publisher
    /// running in this shared host on its next poll. Type.GetType(...) on this string returns null, so
    /// that poll just increments Attempts with an "unknown event type" error instead of successfully
    /// deserializing into (and publishing) an unrelated real event with made-up/empty field values.
    /// </summary>
    private const string FakeEventType = "ZIONShop.Api.IntegrationTests.SystemOps.FakeOutboxEvent, ZIONShop.Api.IntegrationTests.Fake";

    private async Task<Guid> InsertOutboxMessageAsync(int attempts, DateTime? processedOn, string content = "{}")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = FakeEventType,
            Content = content,
            OccurredOn = DateTime.UtcNow,
            Attempts = attempts,
            ProcessedOn = processedOn
        };
        db.Set<OutboxMessage>().Add(message);
        await db.SaveChangesAsync();
        return message.Id;
    }

    [Fact]
    public async Task Modules_Should_ListEveryOutboxAndInboxModule()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/system/modules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ModulesDto>>(ApiEnvelopeJson.Options);
        envelope!.Success.Should().BeTrue();
        envelope.Data!.Outbox.Should().Contain(new[] { "Orders", "Inventory", "Payments", "Promotions" });
        envelope.Data.Inbox.Should().Contain(new[] { "Orders", "Inventory", "Payments", "Promotions", "Cart", "Notifications", "Users" });
    }

    [Fact]
    public async Task PlaceOrder_Should_EventuallyShowAProcessedRow_ForOrdersModule()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        List<OutboxMessageDto>? items = null;
        do
        {
            var response = await admin.GetAsync("/api/v1/admin/system/outbox?module=Orders&status=Processed&pageSize=100");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<OutboxMessageDto>>>(ApiEnvelopeJson.Options);
            items = envelope!.Data;
            if (items is { Count: > 0 }) break;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        items.Should().NotBeNullOrEmpty();
        items!.Should().OnlyContain(m => m.Module == "Orders" && m.Status == "Processed");
    }

    [Fact]
    public async Task ListOutbox_Should_ReturnOnlyFailedRows_When_StatusFilterIsFailed()
    {
        var admin = await AdminClientAsync();
        var failedId = await InsertOutboxMessageAsync(attempts: 10, processedOn: null);
        var processedId = await InsertOutboxMessageAsync(attempts: 0, processedOn: DateTime.UtcNow);

        var response = await admin.GetAsync("/api/v1/admin/system/outbox?module=Orders&status=Failed&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<OutboxMessageDto>>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Should().Contain(m => m.Id == failedId);
        envelope.Data!.Should().NotContain(m => m.Id == processedId);
        envelope.Data!.Should().OnlyContain(m => m.Status == "Failed");
    }

    [Fact]
    public async Task ListOutbox_Should_Return400_When_StatusIsUnknown()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/system/outbox?status=NotARealStatus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListOutbox_Should_Return400_When_ModuleIsUnknown()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/system/outbox?module=NotARealModule");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetOutboxDetail_Should_MaskSensitiveFields()
    {
        var admin = await AdminClientAsync();
        var payload = JsonSerializer.Serialize(new
        {
            orderId = Guid.NewGuid(),
            recipientName = "Nguyen Van A",
            phoneNumber = "0901234567",
            streetAddress = "123 Test Street",
            email = "customer@example.com",
            totalAmount = 100000
        });
        var id = await InsertOutboxMessageAsync(attempts: 0, processedOn: null, content: payload);

        var response = await admin.GetAsync($"/api/v1/admin/system/outbox/Orders/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<OutboxMessageDetailDto>>(ApiEnvelopeJson.Options);
        var detail = envelope!.Data!;
        detail.Payload.Should().NotContain("Nguyen Van A");
        detail.Payload.Should().NotContain("0901234567");
        detail.Payload.Should().NotContain("123 Test Street");
        detail.Payload.Should().NotContain("customer@example.com");
        detail.Payload.Should().Contain("***");
        detail.Payload.Should().Contain("100000"); // non-sensitive fields survive untouched
    }

    [Fact]
    public async Task GetOutboxDetail_Should_Return404_When_MessageDoesNotExist()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync($"/api/v1/admin/system/outbox/Orders/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryOutbox_Should_ResetToPending_When_MessageIsFailed()
    {
        var admin = await AdminClientAsync();
        var id = await InsertOutboxMessageAsync(attempts: 10, processedOn: null);

        var response = await admin.PostAsync($"/api/v1/admin/system/outbox/Orders/{id}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<RetryResultDto>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Id.Should().Be(id);
        envelope.Data!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task RetryOutbox_Should_Return409_When_MessageIsNotFailed()
    {
        var admin = await AdminClientAsync();
        var id = await InsertOutboxMessageAsync(attempts: 0, processedOn: DateTime.UtcNow);

        var response = await admin.PostAsync($"/api/v1/admin/system/outbox/Orders/{id}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RetryOutbox_Should_Return404_When_MessageDoesNotExist()
    {
        var admin = await AdminClientAsync();

        var response = await admin.PostAsync($"/api/v1/admin/system/outbox/Orders/{Guid.NewGuid()}/retry", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RetryFailedOutbox_Should_RetryOnlyFailedRows_ForTheGivenModule()
    {
        var admin = await AdminClientAsync();
        var failedId1 = await InsertOutboxMessageAsync(attempts: 10, processedOn: null);
        var failedId2 = await InsertOutboxMessageAsync(attempts: 10, processedOn: null);
        _ = await InsertOutboxMessageAsync(attempts: 0, processedOn: DateTime.UtcNow);

        var response = await admin.PostAsync("/api/v1/admin/system/outbox/retry-failed?module=Orders", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<RetryFailedResultDto>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Retried.Should().BeGreaterThanOrEqualTo(2);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var rows = await db.Set<OutboxMessage>().Where(m => m.Id == failedId1 || m.Id == failedId2).ToListAsync();
        rows.Should().OnlyContain(m => m.Attempts == 0 && m.Error == null);
    }

    [Fact]
    public async Task Inbox_Should_ShowARow_After_ARealConsumerProcessesAnEvent()
    {
        var admin = await AdminClientAsync();
        var eventBus = _factory.Services.GetRequiredService<IEventBus>();
        var @event = new OrderCancelledIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), "integration test");

        await eventBus.PublishAsync(@event);

        using var scope = _factory.Services.CreateScope();
        var notificationsDb = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var rowExists = await notificationsDb.Set<ZIONShop.EventBus.Inbox.InboxMessage>()
            .AnyAsync(m => m.MessageId == @event.EventId);
        rowExists.Should().BeTrue();

        var response = await admin.GetAsync("/api/v1/admin/system/inbox?module=Notifications&pageSize=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<InboxMessageDto>>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Should().Contain(m => m.MessageId == @event.EventId && m.Module == "Notifications" && m.EventType == nameof(OrderCancelledIntegrationEvent));
    }

    [Fact]
    public async Task Inbox_Should_Return400_When_ModuleIsUnknown()
    {
        var admin = await AdminClientAsync();

        var response = await admin.GetAsync("/api/v1/admin/system/inbox?module=NotARealModule");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Outbox_Should_Return403_ForCustomer()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await customer.GetAsync("/api/v1/admin/system/outbox");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Outbox_Should_Return401_ForAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/system/outbox");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

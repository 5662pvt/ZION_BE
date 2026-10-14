using System.Text.Json;

namespace ZIONShop.Payments.Application.Services;

public record FakeWebhookPayload(Guid EventId, string IntentId, string Result, string? Reason);

public static class FakeWebhookJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

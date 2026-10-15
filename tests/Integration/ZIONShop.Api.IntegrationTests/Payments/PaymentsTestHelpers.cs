using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Payments;

public record PaymentIntentData(Guid PaymentId, string IntentId, Guid OrderId, string Status, decimal Amount, string Currency, string RedirectUrl, DateTime ExpiresAt, string Channel);

public static class PaymentsTestHelpers
{
    /// <summary>Must match Payments:Fake:WebhookSecret in appsettings.Development.json — ApiFactory runs as Development and does not override it.</summary>
    public const string DevWebhookSecret = "dev-only-fake-payments-webhook-secret-zionshop";

    private static readonly JsonSerializerOptions WebhookJsonOptions = new(JsonSerializerDefaults.Web);

    public static string ComputeSignature(string rawBody, string secret = DevWebhookSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
    }

    public static async Task<HttpResponseMessage> CreatePaymentIntentRawAsync(HttpClient client, Guid orderId, string idempotencyKey, string? channel = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/intents")
        {
            Content = JsonContent.Create(new { orderId, channel })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    public static async Task<PaymentIntentData> CreatePaymentIntentAsync(HttpClient client, Guid orderId, string idempotencyKey, string? channel = null)
    {
        var response = await CreatePaymentIntentRawAsync(client, orderId, idempotencyKey, channel);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PaymentIntentData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> GetPaymentIntentRawAsync(HttpClient client, string intentId)
        => await client.GetAsync($"/api/v1/payments/intents/{intentId}");

    public static async Task<PaymentIntentData> GetPaymentIntentAsync(HttpClient client, string intentId)
    {
        var response = await GetPaymentIntentRawAsync(client, intentId);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PaymentIntentData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> SimulateFakePaymentRawAsync(HttpClient client, string intentId, string result)
        => await client.PostAsJsonAsync($"/api/v1/payments/fake/{intentId}/simulate", new { result });

    public static async Task<PaymentIntentData> SimulateFakePaymentAsync(HttpClient client, string intentId, string result)
    {
        var response = await SimulateFakePaymentRawAsync(client, intentId, result);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PaymentIntentData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> PostFakeWebhookRawAsync(HttpClient anonymousClient, Guid eventId, string intentId, string result, string? reason = null, string? signatureOverride = null)
    {
        var rawBody = JsonSerializer.Serialize(new { eventId, intentId, result, reason }, WebhookJsonOptions);
        var signature = signatureOverride ?? ComputeSignature(rawBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/fake")
        {
            Content = new StringContent(rawBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Fake-Signature", signature);
        return await anonymousClient.SendAsync(request);
    }

    public static async Task<PaymentIntentData> WaitForPaymentStatusAsync(HttpClient client, string intentId, string expectedStatus, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        PaymentIntentData last;
        do
        {
            last = await GetPaymentIntentAsync(client, intentId);
            if (last.Status == expectedStatus) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Payment intent {intentId} did not reach status '{expectedStatus}' within {timeout}. Last observed status: '{last.Status}'.");
    }
}

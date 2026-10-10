using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Notifications;

public record NotificationLogData(Guid Id, Guid EventId, string Type, string Channel, Guid UserId, Guid OrderId, string Recipient, string Subject, string Status, string? Error, DateTime CreatedAt, DateTime? SentAt);

public static class NotificationsTestHelpers
{
    public static async Task<List<NotificationLogData>> GetNotificationLogsAsync(HttpClient admin, Guid orderId)
    {
        var response = await admin.GetAsync($"/api/v1/admin/notifications?orderId={orderId}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<NotificationLogData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>
    /// Notification handlers run off the transactional outbox (same as order status transitions),
    /// so the log row settles asynchronously after the triggering order action returns.
    /// </summary>
    public static async Task<NotificationLogData> WaitForNotificationLogAsync(HttpClient admin, Guid orderId, string type, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            var logs = await GetNotificationLogsAsync(admin, orderId);
            var match = logs.FirstOrDefault(l => l.Type == type);
            if (match is not null) return match;
            await Task.Delay(500);
        }

        throw new TimeoutException($"No notification log of type '{type}' appeared for order {orderId} within {timeout}.");
    }
}

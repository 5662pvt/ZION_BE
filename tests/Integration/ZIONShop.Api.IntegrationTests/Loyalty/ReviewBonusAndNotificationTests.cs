using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Api.IntegrationTests.Notifications;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Reviews.ReviewsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Loyalty;

[Collection(ApiTestCollection.Name)]
public class ReviewBonusAndNotificationTests
{
    private readonly ApiFactory _factory;

    public ReviewBonusAndNotificationTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private static async Task<LoyaltySummaryData> GetMyLoyaltyAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/loyalty/me");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoyaltySummaryData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    private static async Task<List<LoyaltyTransactionData>> GetMyLoyaltyTransactionsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/loyalty/me/transactions");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<LoyaltyTransactionData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>Same rationale as LoyaltyTests.WaitForLoyaltyBalanceAsync: the review bonus is earned
    /// asynchronously through Loyalty's own outbox, settling after CreateReview's HTTP response returns.</summary>
    private static async Task<LoyaltySummaryData> WaitForLoyaltyBalanceAsync(HttpClient client, Func<int, bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        LoyaltySummaryData last;
        do
        {
            last = await GetMyLoyaltyAsync(client);
            if (predicate(last.Balance)) return last;
            await Task.Delay(500);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException($"Loyalty balance did not reach the expected state within {timeout}. Last observed: {last.Balance}.");
    }

    private static async Task<List<UserNotificationFeedData>> GetFeedAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/notifications/feed");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<UserNotificationFeedData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    /// <summary>The review-sourced PointsEarned notification carries no OrderId (ReviewCreatedIntegrationEvent
    /// has no order-scoped routing in the feed), so this polls by Type alone rather than (orderId, type)
    /// like NotificationsFeedTests.WaitForFeedEntryAsync does.</summary>
    private static async Task<UserNotificationFeedData> WaitForFeedEntryOfTypeAsync(HttpClient client, string type, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (DateTime.UtcNow < deadline)
        {
            var feed = await GetFeedAsync(client);
            var match = feed.FirstOrDefault(n => n.Type == type);
            if (match is not null) return match;
            await Task.Delay(500);
        }

        throw new TimeoutException($"No feed entry of type '{type}' appeared within {timeout}.");
    }

    [Fact]
    public async Task ReviewingPurchasedProduct_Should_EarnReviewBonus_And_NotifyCustomer_Once()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null)).EnsureSuccessStatusCode();
        await WaitForOrderStatusAsync(customer, placed.Id, "Completed");

        // Order completion earns its own (1% of subtotal) bonus -- snapshot the balance after that settles
        // so the assertion below isolates the +100 Xu review bonus specifically.
        var balanceAfterOrder = (await WaitForLoyaltyBalanceAsync(customer, b => b > 0)).Balance;

        await CreateReviewAsync(customer, productId, 5, "Great", "Really great product, highly recommend it.");

        var summary = await WaitForLoyaltyBalanceAsync(customer, balance => balance == balanceAfterOrder + 100);
        summary.Balance.Should().Be(balanceAfterOrder + 100);

        var transactions = await GetMyLoyaltyTransactionsAsync(customer);
        transactions.Should().ContainSingle(t => t.Type == "Earn" && t.Points == 100 && t.OrderId == null);

        var feedEntry = await WaitForFeedEntryOfTypeAsync(customer, "PointsEarned");
        feedEntry.Message.Should().Contain("100");

        // Reviews forbids a second review of the same product by the same user (Reviews.AlreadyReviewed,
        // 409) regardless of order -- so no second review bonus can ever be earned for this (user, product).
        var secondReviewResponse = await CreateReviewRawAsync(customer, productId, 4, null, "Another review attempt for the same product.");
        ((int)secondReviewResponse.StatusCode).Should().Be(409);

        var finalTransactions = await GetMyLoyaltyTransactionsAsync(customer);
        finalTransactions.Count(t => t.Type == "Earn" && t.Points == 100 && t.OrderId == null).Should().Be(1);
    }
}

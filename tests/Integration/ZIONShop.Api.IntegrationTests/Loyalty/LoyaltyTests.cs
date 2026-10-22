using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Loyalty.Application.Services;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Loyalty;

public record LoyaltySummaryData(int Balance, int ExpiringSoonPoints, DateTime? ExpiringSoonDate, int MaxRedeemPercent, int EarnRatePercent, int ReviewBonus);
public record LoyaltyTransactionData(Guid Id, string Type, int Points, int BalanceAfter, Guid? OrderId, string Description, DateTime CreatedAt);

[Collection(ApiTestCollection.Name)]
public class LoyaltyTests
{
    private readonly ApiFactory _factory;

    public LoyaltyTests(ApiFactory factory) => _factory = factory;

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

    /// <summary>Earn/revoke/refund flow through Loyalty's own outbox (see CheckoutTestHelpers.WaitForOrderStatusAsync
    /// for the same rationale), so the balance settles asynchronously after the triggering request returns.</summary>
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

    [Fact]
    public async Task CompleteOrder_Should_EarnOnePercentOfSubtotal_And_WriteLedgerEntry()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin, price: 500_000m);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null)).EnsureSuccessStatusCode();
        await WaitForOrderStatusAsync(customer, placed.Id, "Completed");

        var expectedPoints = (int)Math.Floor(placed.Subtotal * 1 / 100m);
        var summary = await WaitForLoyaltyBalanceAsync(customer, balance => balance == expectedPoints);
        summary.Balance.Should().Be(expectedPoints);

        var transactions = await GetMyLoyaltyTransactionsAsync(customer);
        transactions.Should().ContainSingle(t => t.Type == "Earn" && t.OrderId == placed.Id && t.Points == expectedPoints);
    }

    [Fact]
    public async Task AdminAdjust_Should_IncreaseThenDecreaseBalance()
    {
        var admin = await AdminClientAsync();
        var (customer, _, userId) = await CreateCustomerWithAddressAsync(_factory);

        var increaseResponse = await admin.PostAsJsonAsync($"/api/v1/admin/loyalty/users/{userId}/adjust", new { delta = 500, reason = "Khuyến mãi" });
        increaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var decreaseResponse = await admin.PostAsJsonAsync($"/api/v1/admin/loyalty/users/{userId}/adjust", new { delta = -100, reason = "Điều chỉnh lỗi" });
        decreaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var summary = await GetMyLoyaltyAsync(customer);
        summary.Balance.Should().Be(400);
    }

    [Fact]
    public async Task AdminAdjust_Should_ReturnBadRequest_When_DeductingMoreThanBalance()
    {
        var admin = await AdminClientAsync();
        var (_, _, userId) = await CreateCustomerWithAddressAsync(_factory);
        (await admin.PostAsJsonAsync($"/api/v1/admin/loyalty/users/{userId}/adjust", new { delta = 100, reason = "seed" })).EnsureSuccessStatusCode();

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/loyalty/users/{userId}/adjust", new { delta = -1000, reason = "too much" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors!.Should().Contain(e => e.Code == "Loyalty.InsufficientPoints");
    }

    [Fact]
    public async Task AdminLoyaltyEndpoints_Should_ReturnForbidden_When_CallerIsCustomer()
    {
        var (customer, _, userId) = await CreateCustomerWithAddressAsync(_factory);

        var response = await customer.GetAsync($"/api/v1/admin/loyalty/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExpiryProcessor_Should_ZeroOutBackdatedLot_And_DropBalance()
    {
        var userId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
            var account = PointAccount.Create(userId);
            account.Increase(200);
            db.PointAccounts.Add(account);
            db.PointLots.Add(PointLot.Create(userId, PointLotSource.AdminAdjust, Guid.NewGuid().ToString(), 200, DateTime.UtcNow.AddMonths(-13), DateTime.UtcNow.AddDays(-1)));
            await db.SaveChangesAsync();

            var processor = scope.ServiceProvider.GetRequiredService<LoyaltyExpiryProcessor>();
            await processor.ExpireLotsAsync();
        }

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
        (await verifyDb.PointAccounts.SingleAsync(a => a.Id == userId)).Balance.Should().Be(0);
    }
}

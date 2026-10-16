using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Reviews.ReviewsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Reviews;

public record MyReviewData(Guid Id, Guid ProductId, string ProductName, string? ProductSlug, int Rating, string? Title, string Content, string Status, string? HiddenReason, DateTime CreatedAt);

[Collection(ApiTestCollection.Name)]
public class MyReviewsTests
{
    private readonly ApiFactory _factory;

    public MyReviewsTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(HttpClient Admin, Guid ProductId, HttpClient Customer, Guid OrderId)> CompleteAnOrderAsync()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 5);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);
        var placed = await PlaceOrderAsync(customer, addressId, "COD");

        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var shipResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/ship", null);
        shipResponse.EnsureSuccessStatusCode();

        var completeResponse = await admin.PostAsync($"/api/v1/orders/{placed.Id}/complete", null);
        completeResponse.EnsureSuccessStatusCode();

        return (admin, productId, customer, placed.Id);
    }

    private static async Task<List<MyReviewData>> GetMineAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/reviews/mine");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<MyReviewData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    [Fact]
    public async Task GetMine_Should_IncludeHiddenReview_With_HiddenStatus()
    {
        var (admin, productId, customer, _) = await CompleteAnOrderAsync();
        var review = await CreateReviewAsync(customer, productId, 5, "Excellent", "Excellent product, works exactly as described.");

        await HideReviewAsync(admin, review.Id, "Policy violation");

        var mine = await GetMineAsync(customer);

        mine.Should().ContainSingle(r => r.Id == review.Id && r.Status == "Hidden" && r.HiddenReason == "Policy violation");
    }

    [Fact]
    public async Task GetMine_Should_NotShowAnotherCustomersReviews()
    {
        var (_, productId, customerA, _) = await CompleteAnOrderAsync();
        await CreateReviewAsync(customerA, productId, 5, "Nice", "Nice product overall, would recommend buying it.");

        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var mineForB = await GetMineAsync(customerB);

        mineForB.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMine_Should_ReturnUnauthorized_When_Anonymous()
    {
        var anonymous = _factory.CreateClient();
        ((int)(await anonymous.GetAsync("/api/v1/reviews/mine")).StatusCode).Should().Be(401);
    }
}

using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Reviews.ReviewsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Reviews;

[Collection(ApiTestCollection.Name)]
public class ReviewsFlowTests
{
    private readonly ApiFactory _factory;

    public ReviewsFlowTests(ApiFactory factory) => _factory = factory;

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

    [Fact]
    public async Task CreateReview_Should_ReturnForbidden_When_CustomerHasNotPurchasedProduct()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);

        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await CreateReviewRawAsync(customer, productId, 5, "Great", "Never actually bought this product at all.");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task Eligibility_Should_ReturnFalseWithReason_When_CustomerHasNotPurchasedProduct()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var eligibility = await GetEligibilityAsync(customer, productId);

        eligibility.CanReview.Should().BeFalse();
        eligibility.Reason.Should().Be("Bạn cần mua và nhận hàng sản phẩm này trước khi đánh giá");
        eligibility.MyReview.Should().BeNull();
    }

    [Fact]
    public async Task ReviewLifecycle_Should_UpdateSummaryAndVisibility_Through_CreateDuplicateHideShow()
    {
        var (admin, productId, customer, _) = await CompleteAnOrderAsync();

        var eligibility = await GetEligibilityAsync(customer, productId);
        eligibility.CanReview.Should().BeTrue();

        var review = await CreateReviewAsync(customer, productId, 5, "Excellent", "Excellent product, works exactly as described.");
        review.Rating.Should().Be(5);
        review.IsVerifiedPurchase.Should().BeTrue();

        var summary = await GetSummaryAsync(customer, productId);
        summary.Count.Should().Be(1);
        summary.Average.Should().Be(5m);
        summary.Distribution["5"].Should().Be(1);

        var duplicateResponse = await CreateReviewRawAsync(customer, productId, 4, null, "Trying to submit a second review for the same product.");
        ((int)duplicateResponse.StatusCode).Should().Be(409);

        var publicReviews = await GetProductReviewsAsync(_factory.CreateClient(), productId);
        publicReviews.Should().ContainSingle(r => r.Id == review.Id);

        await HideReviewAsync(admin, review.Id, "Policy violation");

        var hiddenPublicReviews = await GetProductReviewsAsync(_factory.CreateClient(), productId);
        hiddenPublicReviews.Should().BeEmpty();

        var hiddenSummary = await GetSummaryAsync(_factory.CreateClient(), productId);
        hiddenSummary.Count.Should().Be(0);
        hiddenSummary.Average.Should().Be(0m);

        var eligibilityWhileHidden = await GetEligibilityAsync(customer, productId);
        eligibilityWhileHidden.CanReview.Should().BeFalse();
        eligibilityWhileHidden.MyReview.Should().NotBeNull();
        eligibilityWhileHidden.MyReview!.Id.Should().Be(review.Id);

        await ShowReviewAsync(admin, review.Id);

        var restoredPublicReviews = await GetProductReviewsAsync(_factory.CreateClient(), productId);
        restoredPublicReviews.Should().ContainSingle(r => r.Id == review.Id);

        var restoredSummary = await GetSummaryAsync(_factory.CreateClient(), productId);
        restoredSummary.Count.Should().Be(1);
        restoredSummary.Average.Should().Be(5m);
    }

    [Fact]
    public async Task GetSummaries_Should_ReturnZero_When_ProductHasNoReviews()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);

        var results = await GetSummariesAsync(_factory.CreateClient(), new[] { productId });

        var result = results.Should().ContainSingle(r => r.ProductId == productId).Subject;
        result.Average.Should().Be(0m);
        result.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetSummaries_Should_ReturnBatchResults_When_MultipleProductsHaveReviews()
    {
        var (_, productIdA, customerA, _) = await CompleteAnOrderAsync();
        await CreateReviewAsync(customerA, productIdA, 4, null, "A solid product overall, would buy it again.");

        var adminB = await AdminClientAsync();
        var (productIdB, _) = await CreatePublishedProductAsync(adminB);

        var results = await GetSummariesAsync(_factory.CreateClient(), new[] { productIdA, productIdB });

        results.Should().Contain(r => r.ProductId == productIdA && r.Count == 1 && r.Average == 4m);
        results.Should().Contain(r => r.ProductId == productIdB && r.Count == 0);
    }
}

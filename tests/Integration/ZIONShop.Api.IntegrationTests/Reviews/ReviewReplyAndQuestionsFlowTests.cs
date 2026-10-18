using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Reviews.ReviewsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Reviews;

[Collection(ApiTestCollection.Name)]
public class ReviewReplyAndQuestionsFlowTests
{
    private readonly ApiFactory _factory;

    public ReviewReplyAndQuestionsFlowTests(ApiFactory factory) => _factory = factory;

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
    public async Task ReplyFlow_Should_ShowReplyOnPublicEndpoint_After_AdminReplies()
    {
        var (admin, productId, customer, _) = await CompleteAnOrderAsync();
        var review = await CreateReviewAsync(customer, productId, 5, "Excellent", "Excellent product, works exactly as described.");

        var replied = await ReplyToReviewAsync(admin, review.Id, "Thank you for your purchase!");
        replied.ReplyContent.Should().Be("Thank you for your purchase!");
        replied.RepliedAt.Should().NotBeNull();

        var publicReviews = await GetProductReviewsAsync(_factory.CreateClient(), productId);
        var publicReview = publicReviews.Should().ContainSingle(r => r.Id == review.Id).Subject;
        publicReview.ReplyContent.Should().Be("Thank you for your purchase!");
    }

    [Fact]
    public async Task ReplyFlow_Should_RemoveReply_When_AdminDeletesReply()
    {
        var (admin, productId, customer, _) = await CompleteAnOrderAsync();
        var review = await CreateReviewAsync(customer, productId, 5, "Excellent", "Excellent product, works exactly as described.");
        await ReplyToReviewAsync(admin, review.Id, "Thank you!");

        var removeResponse = await RemoveReviewReplyRawAsync(admin, review.Id);
        removeResponse.EnsureSuccessStatusCode();

        var publicReviews = await GetProductReviewsAsync(_factory.CreateClient(), productId);
        var publicReview = publicReviews.Should().ContainSingle(r => r.Id == review.Id).Subject;
        publicReview.ReplyContent.Should().BeNull();
    }

    [Fact]
    public async Task ReplyFlow_Should_ReturnForbidden_When_CustomerTriesToReply()
    {
        var (_, productId, customer, _) = await CompleteAnOrderAsync();
        var review = await CreateReviewAsync(customer, productId, 5, "Excellent", "Excellent product, works exactly as described.");

        var response = await ReplyToReviewRawAsync(customer, review.Id, "I am not staff");

        ((int)response.StatusCode).Should().Be(403);
    }

    [Fact]
    public async Task QuestionFlow_Should_ShowAnswer_After_CustomerAsksAndAdminAnswers()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var question = await AskQuestionAsync(customer, productId, "Does this come in other colors?");
        question.AnswerContent.Should().BeNull();

        var answered = await AnswerQuestionAsync(admin, question.Id, "Yes, blue and black.");
        answered.AnswerContent.Should().Be("Yes, blue and black.");

        var publicQuestions = await GetProductQuestionsAsync(_factory.CreateClient(), productId);
        var publicQuestion = publicQuestions.Should().ContainSingle(q => q.Id == question.Id).Subject;
        publicQuestion.AnswerContent.Should().Be("Yes, blue and black.");
    }

    [Fact]
    public async Task QuestionFlow_Should_AllowOwner_To_DeleteOwnQuestion()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var question = await AskQuestionAsync(customer, productId, "Does this come in other colors?");

        var response = await DeleteQuestionRawAsync(customer, question.Id);
        response.EnsureSuccessStatusCode();

        var publicQuestions = await GetProductQuestionsAsync(_factory.CreateClient(), productId);
        publicQuestions.Should().NotContain(q => q.Id == question.Id);
    }

    [Fact]
    public async Task QuestionFlow_Should_AllowAdmin_To_DeleteAnyQuestion()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var question = await AskQuestionAsync(customer, productId, "Does this come in other colors?");

        var response = await DeleteQuestionRawAsync(admin, question.Id);
        response.EnsureSuccessStatusCode();

        var publicQuestions = await GetProductQuestionsAsync(_factory.CreateClient(), productId);
        publicQuestions.Should().NotContain(q => q.Id == question.Id);
    }

    [Fact]
    public async Task QuestionFlow_Should_ReturnForbidden_When_DifferentCustomerTriesToDelete()
    {
        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        var (customerA, _, _) = await CreateCustomerWithAddressAsync(_factory);
        var (customerB, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var question = await AskQuestionAsync(customerA, productId, "Does this come in other colors?");

        var response = await DeleteQuestionRawAsync(customerB, question.Id);

        ((int)response.StatusCode).Should().Be(403);
    }
}

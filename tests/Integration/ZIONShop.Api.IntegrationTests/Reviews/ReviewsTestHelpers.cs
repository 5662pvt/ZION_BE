using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Reviews;

public record ReviewData(Guid Id, Guid ProductId, int Rating, string? Title, string Content, string AuthorName, bool IsVerifiedPurchase, DateTime CreatedAt, DateTime? UpdatedAt, string? ReplyContent, DateTime? RepliedAt);
public record AdminReviewData(Guid Id, Guid ProductId, int Rating, string? Title, string Content, string AuthorName, bool IsVerifiedPurchase, DateTime CreatedAt, DateTime? UpdatedAt, string ProductName, string Status, string? HiddenReason, string? ReplyContent, DateTime? RepliedAt);
public record ReviewSummaryData(Guid ProductId, decimal Average, int Count, Dictionary<string, int> Distribution);
public record ProductRatingData(Guid ProductId, decimal Average, int Count);
public record ReviewEligibilityData(bool CanReview, string? Reason, ReviewData? MyReview);
public record ProductQuestionData(Guid Id, Guid ProductId, Guid UserId, string AuthorName, string Content, DateTime CreatedAt, string? AnswerContent, DateTime? AnsweredAt);

public static class ReviewsTestHelpers
{
    public static async Task<HttpResponseMessage> CreateReviewRawAsync(HttpClient client, Guid productId, int rating, string? title, string content)
        => await client.PostAsJsonAsync($"/api/v1/products/{productId}/reviews", new { rating, title, content });

    public static async Task<ReviewData> CreateReviewAsync(HttpClient client, Guid productId, int rating, string? title, string content)
    {
        var response = await CreateReviewRawAsync(client, productId, rating, title, content);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ReviewData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<ReviewEligibilityData> GetEligibilityAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/products/{productId}/reviews/eligibility");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ReviewEligibilityData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<List<ReviewData>> GetProductReviewsAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/products/{productId}/reviews");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ReviewData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<ReviewSummaryData> GetSummaryAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/products/{productId}/reviews/summary");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ReviewSummaryData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<List<ProductRatingData>> GetSummariesAsync(HttpClient client, IEnumerable<Guid> productIds)
    {
        var qs = string.Join(",", productIds);
        var response = await client.GetAsync($"/api/v1/reviews/summaries?productIds={qs}");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductRatingData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> HideReviewRawAsync(HttpClient admin, Guid reviewId, string? reason = null)
        => await admin.PostAsJsonAsync($"/api/v1/admin/reviews/{reviewId}/hide", new { reason });

    public static async Task<AdminReviewData> HideReviewAsync(HttpClient admin, Guid reviewId, string? reason = null)
    {
        var response = await HideReviewRawAsync(admin, reviewId, reason);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AdminReviewData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<AdminReviewData> ShowReviewAsync(HttpClient admin, Guid reviewId)
    {
        var response = await admin.PostAsync($"/api/v1/admin/reviews/{reviewId}/show", null);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AdminReviewData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> ReplyToReviewRawAsync(HttpClient admin, Guid reviewId, string content)
        => await admin.PostAsJsonAsync($"/api/v1/admin/reviews/{reviewId}/reply", new { content });

    public static async Task<AdminReviewData> ReplyToReviewAsync(HttpClient admin, Guid reviewId, string content)
    {
        var response = await ReplyToReviewRawAsync(admin, reviewId, content);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AdminReviewData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> RemoveReviewReplyRawAsync(HttpClient admin, Guid reviewId)
        => await admin.DeleteAsync($"/api/v1/admin/reviews/{reviewId}/reply");

    public static async Task<List<ProductQuestionData>> GetProductQuestionsAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/v1/products/{productId}/questions");
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ProductQuestionData>>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> AskQuestionRawAsync(HttpClient client, Guid productId, string content)
        => await client.PostAsJsonAsync($"/api/v1/products/{productId}/questions", new { content });

    public static async Task<ProductQuestionData> AskQuestionAsync(HttpClient client, Guid productId, string content)
    {
        var response = await AskQuestionRawAsync(client, productId, content);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ProductQuestionData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> AnswerQuestionRawAsync(HttpClient admin, Guid questionId, string content)
        => await admin.PostAsJsonAsync($"/api/v1/admin/questions/{questionId}/answer", new { content });

    public static async Task<ProductQuestionData> AnswerQuestionAsync(HttpClient admin, Guid questionId, string content)
    {
        var response = await AnswerQuestionRawAsync(admin, questionId, content);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<ProductQuestionData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static async Task<HttpResponseMessage> DeleteQuestionRawAsync(HttpClient client, Guid questionId)
        => await client.DeleteAsync($"/api/v1/questions/{questionId}");
}

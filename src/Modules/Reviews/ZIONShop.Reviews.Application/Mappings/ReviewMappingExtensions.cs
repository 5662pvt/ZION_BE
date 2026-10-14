using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Application.Mappings;

public static class ReviewMappingExtensions
{
    public static ReviewDto ToDto(this Review review) => new(
        review.Id,
        review.ProductId,
        review.Rating,
        review.Title,
        review.Content,
        review.AuthorName,
        true,
        review.CreatedDate,
        review.LastModifiedDate);

    public static AdminReviewDto ToAdminDto(this Review review) => new(
        review.Id,
        review.ProductId,
        review.Rating,
        review.Title,
        review.Content,
        review.AuthorName,
        true,
        review.CreatedDate,
        review.LastModifiedDate,
        review.ProductName,
        review.Status.ToString(),
        review.HiddenReason);

    public static ReviewSummaryDto ToSummaryDto(this ProductRatingSummary? summary, Guid productId)
    {
        if (summary is null)
        {
            return new ReviewSummaryDto(productId, 0m, 0, EmptyDistribution());
        }

        var distribution = new Dictionary<string, int>
        {
            ["1"] = summary.Star1,
            ["2"] = summary.Star2,
            ["3"] = summary.Star3,
            ["4"] = summary.Star4,
            ["5"] = summary.Star5
        };

        return new ReviewSummaryDto(productId, summary.Average, summary.Count, distribution);
    }

    public static ProductRatingDto ToRatingDto(this ProductRatingSummary? summary, Guid productId) =>
        summary is null ? new ProductRatingDto(productId, 0m, 0) : new ProductRatingDto(productId, summary.Average, summary.Count);

    private static IReadOnlyDictionary<string, int> EmptyDistribution() => new Dictionary<string, int>
    {
        ["1"] = 0,
        ["2"] = 0,
        ["3"] = 0,
        ["4"] = 0,
        ["5"] = 0
    };
}

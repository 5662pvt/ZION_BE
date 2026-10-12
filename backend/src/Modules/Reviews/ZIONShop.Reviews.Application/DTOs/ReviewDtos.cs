namespace ZIONShop.Reviews.Application.DTOs;

public record ReviewDto(
    Guid Id,
    Guid ProductId,
    int Rating,
    string? Title,
    string Content,
    string AuthorName,
    bool IsVerifiedPurchase,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminReviewDto(
    Guid Id,
    Guid ProductId,
    int Rating,
    string? Title,
    string Content,
    string AuthorName,
    bool IsVerifiedPurchase,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string ProductName,
    string Status,
    string? HiddenReason);

public record ReviewSummaryDto(Guid ProductId, decimal Average, int Count, IReadOnlyDictionary<string, int> Distribution);

public record ProductRatingDto(Guid ProductId, decimal Average, int Count);

public record ReviewEligibilityDto(bool CanReview, string? Reason, ReviewDto? MyReview);
